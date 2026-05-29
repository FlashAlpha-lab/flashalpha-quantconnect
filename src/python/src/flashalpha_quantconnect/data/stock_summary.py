"""Stock-summary composite bar.

Mirrors ``flashalpha_historical.types.StockSummaryResponse`` from
``GET /v1/stock/{symbol}/summary?at=...`` — the "single best snapshot"
endpoint. Carries the full picture for a name as of the requested
timestamp: price, volatility (ATM IV, HV20/60, VRP, 25-delta skew, IV
term structure), aggregated options flow, dealer-exposure block (greeks,
walls, gamma flip, max pain, hedging estimates, 0DTE, top strikes), and
macro context (VIX/VVIX/SKEW/SPX/MOVE plus term structure).

Historical-specific gaps (from the SDK docstring):

- ``options_flow.total_call_volume`` / ``total_put_volume`` /
  ``pc_ratio_volume`` are always ``0`` / ``None`` — no minute volume on
  replay.
- ``macro.vix_futures`` is always ``None`` — CME futures aren't
  historically reconstructible from minute data.
- ``macro.fear_and_greed`` is always ``None`` — the CNN index isn't
  archived.
"""

from __future__ import annotations

from typing import Any, Dict, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class StockSummaryBar(PythonDataBase):
    """Stock-summary composite bar.

    Mirrors ``flashalpha_historical.types.StockSummaryResponse`` from
    ``GET /v1/stock/{symbol}/summary?at=...``.

    Subscribe with: ``algo.AddData(StockSummaryBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_stock_summary(algo, "SPY")``.

    Top-level blocks (``PriceQuote``, ``Volatility``, ``OptionsFlow``,
    ``Exposure``, ``Macro``) land on the bar as raw ``dict`` values —
    drill in with ``bar.Volatility["atm_iv"]`` etc.

    Note: ``PriceQuote`` is the SDK's ``price`` block, renamed from
    ``Price`` to avoid colliding with the LEAN ``BaseData.Price`` scalar.
    """

    Ticker: str = ""
    AsOf: str = ""
    MarketOpen: Optional[bool] = None
    PriceQuote: Optional[Dict[str, Any]] = None
    Volatility: Optional[Dict[str, Any]] = None
    OptionsFlow: Optional[Dict[str, Any]] = None
    Exposure: Optional[Dict[str, Any]] = None
    Macro: Optional[Dict[str, Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
        "PriceQuote": "price",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("stock/summary", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(StockSummaryBar, line, config.Symbol, date)
