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


def source_for(endpoint: str, symbol: Any, date: datetime) -> Any:
    """Eagerly fetch the JSON for (endpoint, ticker, date), cache it, return
    a sentinel ``SubscriptionDataSource`` that ``parse`` will resolve via
    the cache key."""
    from QuantConnect import SubscriptionTransportMedium
    from QuantConnect.Data import SubscriptionDataSource, FileFormat

    ticker = symbol.Value
    key = _make_key(endpoint, ticker, date)
    payload = _get_client().fetch_json(endpoint=endpoint, ticker=ticker, at=date)

    with _cache_lock:
        _cache[key] = json.dumps(payload)

    return SubscriptionDataSource(
        f"{_SENTINEL_PREFIX}{key}",
        SubscriptionTransportMedium.Rest,
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
