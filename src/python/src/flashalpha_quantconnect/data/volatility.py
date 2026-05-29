"""Volatility-analytics bars: Volatility + AdvVolatility.

Each class mirrors the matching SDK response type from
``flashalpha_historical.types``. JSON field names are mapped onto bar
properties by ``data.source.parse`` — snake_case JSON keys auto-map to
PascalCase property names, and explicit overrides go through the
``_field_aliases`` class attribute (Python equivalent of C#'s
``[JsonPropertyName]``).

Both bars expose nested response blocks as raw ``dict`` / ``list`` values
on the bar — Python users drill in with subscript access. This mirrors
the shape the SDK returns (``flashalpha_historical`` uses ``TypedDict``
aliases that resolve to plain dicts at runtime).
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class VolatilityBar(PythonDataBase):
    """Volatility-analytics bar.

    Mirrors ``flashalpha_historical.types.VolatilityResponse`` from
    ``GET /v1/volatility/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(VolatilityBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_volatility(algo, "SPY")``.

    Nested blocks (``RealizedVol``, ``IvRvSpreads``, ``TermStructure``,
    ``IvDispersion``, ``PutCallProfile``, ``OiConcentration``,
    ``Liquidity``) and per-row lists (``SkewProfiles``, ``GexByDte``,
    ``ThetaByDte``, ``HedgingScenarios``) are exposed as raw ``dict`` /
    ``list`` values. Drill in with ``bar.IvRvSpreads["vrp_20d"]`` etc.

    Top-level scalar ``AtmIv`` is the at-the-money implied volatility —
    note it lives at the response root, NOT under ``IvRvSpreads``.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    MarketOpen: Optional[bool] = None
    RealizedVol: Optional[Dict[str, Any]] = None
    AtmIv: Optional[float] = None
    IvRvSpreads: Optional[Dict[str, Any]] = None
    SkewProfiles: Optional[List[Any]] = None
    TermStructure: Optional[Dict[str, Any]] = None
    IvDispersion: Optional[Dict[str, Any]] = None
    GexByDte: Optional[List[Any]] = None
    ThetaByDte: Optional[List[Any]] = None
    PutCallProfile: Optional[Dict[str, Any]] = None
    OiConcentration: Optional[Dict[str, Any]] = None
    HedgingScenarios: Optional[List[Any]] = None
    Liquidity: Optional[Dict[str, Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("volatility", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(VolatilityBar, line, config.Symbol, date)


class AdvVolatilityBar(PythonDataBase):
    """Advanced volatility-analytics bar.

    Mirrors ``flashalpha_historical.types.AdvVolatilityResponse`` from
    ``GET /v1/adv_volatility/{symbol}?at=...``. Cold-cache responses can
    take ~1.5s.

    Subscribe with: ``algo.AddData(AdvVolatilityBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_adv_volatility(algo, "SPY")``.

    Carries per-expiry SVI parameters (``SviParameters``), forward prices
    (``ForwardPrices``), the full total-variance surface
    (``TotalVarianceSurface`` — log-moneyness × tenor grid plus implied-vol
    grid), arbitrage flags (``ArbitrageFlags``), variance swap fair values
    (``VarianceSwapFairValues``), and second-/third-order greek surfaces
    (``GreeksSurfaces`` — vanna/charm/volga/speed on a strike × expiry grid).
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    MarketOpen: Optional[bool] = None
    SviParameters: Optional[List[Any]] = None
    ForwardPrices: Optional[List[Any]] = None
    TotalVarianceSurface: Optional[Dict[str, Any]] = None
    ArbitrageFlags: Optional[List[Any]] = None
    VarianceSwapFairValues: Optional[List[Any]] = None
    GreeksSurfaces: Optional[Dict[str, Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("adv-volatility", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(AdvVolatilityBar, line, config.Symbol, date)
