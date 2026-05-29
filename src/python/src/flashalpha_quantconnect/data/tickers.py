"""Tickers (coverage / supported-symbols) bar.

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

from typing import Any, List, Optional

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
