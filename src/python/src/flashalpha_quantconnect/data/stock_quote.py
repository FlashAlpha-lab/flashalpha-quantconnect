"""Stock-quote bar (bid/ask/mid/last) from
``GET /v1/stockquote/{symbol}?at=...``.

The historical SDK's ``stock_quote()`` returns a plain ``dict`` (no
typed model), so this bar declares each documented field explicitly.

Wire-format note: the root key is ``ticker`` (NOT ``symbol``) and the
timestamp field is camelCase ``lastUpdate``. The ``_field_aliases``
declaration captures both.
"""

from __future__ import annotations

from typing import Any, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class StockQuoteBar(PythonDataBase):
    """Stock-quote bar — top-of-book bid/ask/mid/last for the underlier.

    Mirrors the JSON response from ``GET /v1/stockquote/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(StockQuoteBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_stock_quote(algo, "SPY")``.

    Invariants:
        ``bar.Bid <= bar.Mid <= bar.Ask`` (within feed precision).
    """

    Ticker: str = ""
    Bid: Optional[float] = None
    Ask: Optional[float] = None
    Mid: Optional[float] = None
    Last: Optional[float] = None
    LastUpdate: str = ""

    _field_aliases = {
        "Ticker": "ticker",
        "LastUpdate": "lastUpdate",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("stock/quote", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(StockQuoteBar, line, config.Symbol, date)
