"""Shared GetSource/Reader plumbing for every FlashAlpha bar (Python).

Implements the Owned-HTTP branch from the auth-mechanism ADR:
- ``source_for(endpoint, symbol, date)`` eagerly fetches via
  ``FlashAlphaHttpClient.fetch_json`` and stashes the JSON in a
  process-local cache keyed by ``"{endpoint}|{ticker}|{yyyy-MM-dd}"``,
  then returns a sentinel ``SubscriptionDataSource``.
- ``parse(bar_cls, line, symbol, date)`` resolves the sentinel from the
  cache (or falls back to treating ``line`` as raw JSON), constructs an
  instance of ``bar_cls``, fills snake_case JSON fields into PascalCase
  bar properties.

QC LEAN imports happen lazily inside the functions so this module can be
imported in non-runtime contexts (tests, IDE introspection) without a
LEAN install.
"""

from __future__ import annotations

import json
from datetime import datetime, timedelta
from threading import Lock
from typing import Any, Optional, Type

from ..client import FlashAlphaHttpClient


_SENTINEL_PREFIX = "flashalpha://"
_cache: dict[str, str] = {}
_cache_lock = Lock()
_client: Optional[FlashAlphaHttpClient] = None


def _get_client() -> FlashAlphaHttpClient:
    global _client
    if _client is None:
        _client = FlashAlphaHttpClient()
    return _client


def _make_key(endpoint: str, ticker: str, date: datetime) -> str:
    return f"{endpoint}|{ticker}|{date:%Y-%m-%d}"


def _to_pascal_case(snake: str) -> str:
    return "".join(p.title() for p in snake.split("_"))


def _shift_to_market_hours(date: datetime) -> datetime:
    """LEAN daily-res ticks at midnight UTC, but FlashAlpha's historical API
    only has market-hours data. Shift any midnight-UTC date to 20:00 UTC
    (16:00 ET, NYSE close) so the API returns the session's closing snapshot.

    Dates with non-midnight times pass through unchanged — callers that
    want a specific timestamp (intraday hourly subscriptions, etc.) get
    exactly what they asked for.
    """
    if date.hour == 0 and date.minute == 0 and date.second == 0:
        return date.replace(hour=20, minute=0, second=0)
    return date


def source_for(endpoint: str, symbol: Any, date: datetime) -> Any:
    """Eagerly fetch the JSON for (endpoint, ticker, date), persist it to a
    temp file, return a ``SubscriptionDataSource`` pointing LEAN at the file.

    Sparse data is the expected case for LEAN custom-data subscriptions —
    weekends, holidays, pre-RTH midnight ticks on a Daily resolution, etc.
    When the FlashAlpha API reports no data at the requested timestamp
    (``NoDataError`` / ``NoCoverageError`` / ``InvalidAtError``), we write an
    empty file so ``parse`` returns ``None`` and LEAN skips the bar cleanly.
    The algorithm waits for the next session, no error surfaces.

    Transport: ``LocalFile`` — LEAN's ``RestSubscriptionStreamReader`` would
    try to HTTP-fetch a URL, so a custom in-memory sentinel scheme isn't
    workable. Writing the JSON to a single-line file under the OS tempdir
    and using ``LocalFile`` + ``FileFormat.Csv`` lets LEAN's standard
    line-by-line reader hand the full JSON to ``Reader`` as the ``line``
    argument in one call.
    """
    import os
    import tempfile

    from QuantConnect import SubscriptionTransportMedium
    from QuantConnect.Data import SubscriptionDataSource, FileFormat

    # Import sparsely — the SDK exception classes only need to exist at
    # call time, and importing them at module load would couple this file
    # to the SDK's namespace shape.
    from flashalpha_historical.exceptions import (
        NoDataError, NoCoverageError, InvalidAtError,
    )

    ticker = symbol.Value
    key = _make_key(endpoint, ticker, date)

    api_at = _shift_to_market_hours(date)
    try:
        payload = _get_client().fetch_json(endpoint=endpoint, ticker=ticker, at=api_at)
        line = json.dumps(payload)
    except (NoDataError, NoCoverageError, InvalidAtError):
        line = ""

    # Cache for parse() — handles both the LocalFile path (line == file contents)
    # and any other call shape that might bypass the file (defensive).
    with _cache_lock:
        _cache[key] = line

    # Persist as a single-line file under the OS tempdir. LEAN's LocalFile
    # transport + Csv format reads line-by-line, so a 1-line JSON file
    # yields exactly one Reader call with the full payload as `line`.
    tmp_dir = os.path.join(tempfile.gettempdir(), "flashalpha-quantconnect")
    os.makedirs(tmp_dir, exist_ok=True)
    # Filename includes the cache key — deterministic across runs of the
    # same (endpoint, ticker, date) and avoids tempfile churn.
    safe_key = key.replace("/", "_").replace("|", "_")
    path = os.path.join(tmp_dir, f"{safe_key}.json")
    with open(path, "w", encoding="utf-8") as f:
        f.write(line)

    return SubscriptionDataSource(
        path,
        SubscriptionTransportMedium.LocalFile,
        FileFormat.Csv,
    )


def parse(bar_cls: Type, line: str, symbol: Any, date: datetime) -> Any:
    """Resolve ``line`` (sentinel or raw JSON) into a bar instance of ``bar_cls``.

    Returns ``None`` if the line is empty or the cache miss."""
    payload = _resolve_json(line)
    if not payload:
        return None

    bar = bar_cls()
    bar.Symbol = symbol
    bar.Time = date
    bar.EndTime = date + timedelta(days=1)

    obj = json.loads(payload)
    if not isinstance(obj, dict):
        return bar

    # Collect attribute names declared on the bar SUBCLASS (and any non-LEAN
    # ancestors). We deliberately stop walking at the LEAN base — its
    # ``Symbol`` / ``Time`` / ``EndTime`` / ``Value`` / ``Price`` attributes
    # are already populated above, and the JSON's ``symbol`` key would
    # otherwise auto-route into ``BaseData.Symbol`` and clobber the QC
    # Symbol object with the raw ticker string.
    declared: set[str] = set()
    for cls in bar_cls.__mro__:
        mod = getattr(cls, "__module__", "") or ""
        if mod.startswith("QuantConnect") or cls is object:
            break
        declared.update(vars(cls).keys())
        declared.update(getattr(cls, "__annotations__", {}).keys())

    for snake, value in obj.items():
        prop = _to_pascal_case(snake)
        if prop in declared:
            setattr(bar, prop, value)

    # Honor explicit field-name aliases declared on the bar class.
    # Python equivalent of C#'s [JsonPropertyName] — lets a bar property
    # like ``Ticker`` pick up the JSON ``symbol`` key (since QC LEAN's
    # ``BaseData.Symbol`` collides with the SDK's ``symbol`` field).
    aliases = getattr(bar_cls, "_field_aliases", {})
    for prop, json_key in aliases.items():
        if json_key in obj and hasattr(bar, prop):
            setattr(bar, prop, obj[json_key])
    return bar


def _resolve_json(line: str) -> str:
    if not line:
        return ""
    if not line.startswith(_SENTINEL_PREFIX):
        return line
    key = line[len(_SENTINEL_PREFIX):]
    with _cache_lock:
        return _cache.get(key, "")


def reset() -> None:
    """Clear cache and client — for tests only."""
    global _client
    with _cache_lock:
        _cache.clear()
    _client = None
