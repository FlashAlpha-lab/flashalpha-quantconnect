"""Max-pain bar.

Mirrors ``flashalpha_historical.types.MaxPainResponse`` from
``GET /v1/max-pain/{symbol}?at=...``. Nested objects (``distance``,
``dealer_alignment``, ``expected_move``) and per-strike lists
(``pain_curve``, ``oi_by_strike``, ``max_pain_by_expiration``) land on
the bar as raw ``dict`` / ``list`` values.
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class MaxPainBar(PythonDataBase):
    """Max-pain bar.

    Mirrors ``flashalpha_historical.types.MaxPainResponse`` from
    ``GET /v1/max-pain/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(MaxPainBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_max_pain(algo, "SPY")``.

    When the request resolves to a single expiry, ``MaxPainByExpiration``
    is ``None`` and ``Expiration`` is populated. When the request rolls up
    all expiries, ``Expiration`` is ``None`` and ``MaxPainByExpiration``
    carries one dict per expiry.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    MaxPainStrike: Optional[float] = None
    Distance: Optional[Dict[str, Any]] = None
    Signal: str = ""
    Expiration: str = ""
    PutCallOiRatio: Optional[float] = None
    PainCurve: Optional[List[Any]] = None
    OiByStrike: Optional[List[Any]] = None
    MaxPainByExpiration: Optional[List[Any]] = None
    DealerAlignment: Optional[Dict[str, Any]] = None
    Regime: str = ""
    ExpectedMove: Optional[Dict[str, Any]] = None
    PinProbability: Optional[int] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("max-pain", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(MaxPainBar, line, config.Symbol, date)
