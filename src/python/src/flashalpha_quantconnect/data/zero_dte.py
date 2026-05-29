"""Zero-DTE (same-day-expiry) dealer-positioning bar.

Mirrors ``flashalpha_historical.types.ZeroDteResponse`` from
``GET /v1/exposure/zero-dte/{symbol}?at=...``. The rich nested blocks
(regime, exposures, expected_move, pin_risk, hedging, decay, vol_context,
flow, levels, liquidity, metadata) land on the bar as raw ``dict``
values — same shape the SDK returns.

On names without a same-day expiry the SDK returns a "thin" response —
``NoZeroDte = True``, ``Message`` populated, all nested blocks ``None``,
and ``NextZeroDteExpiry`` pointing at the next available expiry.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class ZeroDteBar(PythonDataBase):
    """Zero-DTE dealer-positioning bar.

    Mirrors ``flashalpha_historical.types.ZeroDteResponse`` from
    ``GET /v1/exposure/zero-dte/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(ZeroDteBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_zero_dte(algo, "SPY")``.

    Nested blocks (``Regime``, ``Exposures``, ``ExpectedMove``, ``PinRisk``,
    ``Hedging``, ``Decay``, ``VolContext``, ``Flow``, ``Levels``,
    ``Liquidity``, ``Metadata``) are exposed as raw ``dict`` values.
    Drill in with ``bar.Exposures["net_gex"]`` etc.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    Expiration: str = ""
    AsOf: str = ""
    MarketOpen: Optional[bool] = None
    TimeToCloseHours: Optional[float] = None
    TimeToClosePct: Optional[float] = None
    Regime: Optional[Dict[str, Any]] = None
    Exposures: Optional[Dict[str, Any]] = None
    ExpectedMove: Optional[Dict[str, Any]] = None
    PinRisk: Optional[Dict[str, Any]] = None
    Hedging: Optional[Dict[str, Any]] = None
    Decay: Optional[Dict[str, Any]] = None
    VolContext: Optional[Dict[str, Any]] = None
    Flow: Optional[Dict[str, Any]] = None
    Levels: Optional[Dict[str, Any]] = None
    Liquidity: Optional[Dict[str, Any]] = None
    Metadata: Optional[Dict[str, Any]] = None
    Strikes: Optional[List[Any]] = None
    Warnings: Optional[List[str]] = None
    NoZeroDte: Optional[bool] = None
    Message: str = ""
    NextZeroDteExpiry: str = ""

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/zero-dte", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(ZeroDteBar, line, config.Symbol, date)
