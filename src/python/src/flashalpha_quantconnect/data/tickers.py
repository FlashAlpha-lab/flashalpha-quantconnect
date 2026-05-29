"""Tickers (coverage / supported-symbols) bar + universe-selection helper.

Mirrors the JSON response from ``GET /v1/tickers`` (list form, no
``symbol`` filter). Each row in ``Tickers`` is a dict with the
underlying symbol plus its first/last covered session and a
healthy-day count.

Special case: this is the only endpoint in the bridge that is NOT
ticker-scoped. The bar still subscribes under a LEAN symbol
(whatever you pass to ``AddData``) — the HTTP client ignores the
ticker when the endpoint slug is ``"tickers"`` and calls
``sdk.tickers(symbol=None)``. Subscribing under different LEAN symbols
just gives you separate cache entries containing the same global
coverage table — usually you'd use a sentinel like ``"_universe"``.
"""

from __future__ import annotations

from datetime import datetime, timedelta
from typing import Any, Callable, Dict, Iterable, List, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class TickersBar(PythonDataBase):
    """Tickers coverage bar.

    Mirrors the JSON response from ``GET /v1/tickers``.

    Subscribe with: ``algo.AddData(TickersBar, "_universe", Resolution.Daily)``
    or the sugar ``add_flashalpha_tickers(algo)``.

    ``Tickers`` is a list of dicts (one per covered symbol), each with
    keys ``symbol`` and ``coverage`` (``{first, last, healthy_days}``).
    ``Count`` mirrors the API's row-count scalar — equal to
    ``len(Tickers)``.
    """

    Tickers: Optional[List[Any]] = None
    Count: Optional[int] = None

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("tickers", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(TickersBar, line, config.Symbol, date)


# ---------------------------------------------------------------------------
# Universe-selection helper
# ---------------------------------------------------------------------------

# Try to inherit from LEAN's UniverseSelectionModel when running inside the
# LEAN runtime; outside it (tests, IDE introspection) fall back to a plain
# ``object`` stand-in. Matches the same defensive-import pattern used by
# ``data._lean_import`` for ``PythonData``.
try:  # pragma: no cover - import guard exercised by LEAN runtime
    from QuantConnect.Algorithm.Framework.Selection import (
        UniverseSelectionModel as _USM,
    )
except Exception:  # pragma: no cover - fallback for non-LEAN environments
    class _USM:  # type: ignore[no-redef]
        """Stand-in base used outside the LEAN runtime. Real LEAN runs
        supply the production base class."""


class FlashAlphaTickersUniverse(_USM):
    """LEAN universe-selection helper backed by the FlashAlpha tickers endpoint.

    On each daily refresh, this model pulls the supported-tickers list
    (with coverage metadata) from the FlashAlpha historical API and emits
    the underlying ticker strings for the rows passing the predicate.

    The predicate sees the raw row dict — exactly the same shape the
    ``flashalpha_historical`` SDK returns. Each row has::

        {"symbol": "SPY", "coverage": {"first": "...", "last": "...", "healthy_days": 123}}

    Usage inside ``Initialize``::

        from flashalpha_quantconnect import FlashAlphaTickersUniverse
        self.AddUniverseSelection(FlashAlphaTickersUniverse(
            filter=lambda row: row.get("coverage", {}).get("healthy_days", 0) > 30
        ))

    Outside LEAN — tests, scripts, REPL — call :meth:`select_tickers`
    directly to introspect the universe.
    """

    def __init__(
        self,
        filter: Optional[Callable[[Dict[str, Any]], bool]] = None,
        client: Optional[Any] = None,
    ):
        # The fallback ``_USM`` has no ctor; the real LEAN base has a
        # parameterless one. Either way ``super().__init__()`` is safe.
        try:
            super().__init__()
        except TypeError:  # pragma: no cover - defensive
            pass
        self._filter = filter or (lambda _row: True)
        self._client = client

    def select_tickers(self, date: datetime) -> List[str]:
        """Algorithm-free selector — does the HTTP fetch + filter and
        returns the ticker strings that survive the predicate.

        Used by :meth:`CreateUniverses` at LEAN-runtime, and directly by
        tests / scripts that want to introspect the universe.
        """
        client = self._client
        if client is None:
            from .source import _get_client

            client = _get_client()

        raw = client.fetch_json(endpoint="tickers", ticker="_universe", at=date)
        rows: Iterable[Dict[str, Any]] = []
        if isinstance(raw, dict):
            value = raw.get("tickers")
            if isinstance(value, list):
                rows = value

        return [
            row["symbol"]
            for row in rows
            if isinstance(row, dict)
            and row.get("symbol")
            and self._filter(row)
        ]

    def CreateUniverses(self, algorithm: Any) -> List[Any]:
        """LEAN entry-point — builds the single ``CustomUniverse`` that
        will be re-evaluated daily.

        Imports happen lazily so the module stays importable outside the
        LEAN runtime. If the LEAN universe API isn't available (e.g.
        running under pure pytest) this returns an empty list rather
        than failing — call :meth:`select_tickers` directly in that case.
        """
        try:
            from QuantConnect import Market, Resolution, SecurityType
            from QuantConnect.Data.UniverseSelection import (
                CustomUniverse,
                SubscriptionDataConfig,
                UniverseSettings,
            )
        except Exception:  # pragma: no cover - non-LEAN fallback
            return []

        # Use SecurityType.Base for the placeholder config — equity-typed
        # placeholders force LEAN to resolve map files at construction
        # time, which is wrong here (we surface the actual equity symbols
        # at selection time).
        symbol = algorithm.Symbol(
            "flashalpha-tickers-universe", SecurityType.Base, Market.USA
        )
        settings = UniverseSettings(
            Resolution.Daily, 1.0, True, False, timedelta(0)
        )
        configuration = SubscriptionDataConfig(
            type(self), symbol, Resolution.Daily, True, True, False, False, False
        )
        return [
            CustomUniverse(
                configuration,
                settings,
                timedelta(days=1),
                lambda dt: self.select_tickers(dt),
            )
        ]
