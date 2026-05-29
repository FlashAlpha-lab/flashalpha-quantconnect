"""Variance-risk-premium (VRP) bar.

Mirrors ``flashalpha_historical.types.VrpResponse`` from
``GET /v1/vrp/{symbol}?at=...``. Nested blocks (``Vrp``, ``Directional``,
``GexConditioned``, ``VannaConditioned``, ``Regime``, ``StrategyScores``,
``Macro``) and the term-VRP list (``TermVrp``) land on the bar as raw
``dict`` / ``list`` values.

Common silent-null traps: ``z_score`` / ``percentile`` live on
``bar.Vrp``; ``net_gex`` lives on ``bar.Regime``; ``harvest_score`` on the
top level refers to ``bar.GexConditioned["harvest_score"]``, while
``NetHarvestScore`` is a separate composite. On historical responses with
short warm-up, ``StrategyScores`` and ``NetHarvestScore`` can be ``None``
— check ``Warnings`` for the reason.

Returns 403 ``tier_restricted`` for anything below Alpha plan.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class VrpBar(PythonDataBase):
    """VRP bar.

    Mirrors ``flashalpha_historical.types.VrpResponse`` from
    ``GET /v1/vrp/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(VrpBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_vrp(algo, "SPY")``.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    MarketOpen: Optional[bool] = None
    Vrp: Optional[Dict[str, Any]] = None
    VarianceRiskPremium: Optional[float] = None
    ConvexityPremium: Optional[float] = None
    FairVol: Optional[float] = None
    Directional: Optional[Dict[str, Any]] = None
    TermVrp: Optional[List[Any]] = None
    GexConditioned: Optional[Dict[str, Any]] = None
    VannaConditioned: Optional[Dict[str, Any]] = None
    Regime: Optional[Dict[str, Any]] = None
    StrategyScores: Optional[Dict[str, Any]] = None
    NetHarvestScore: Optional[int] = None
    DealerFlowRisk: Optional[int] = None
    Warnings: Optional[List[str]] = None
    Macro: Optional[Dict[str, Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("vrp", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(VrpBar, line, config.Symbol, date)
