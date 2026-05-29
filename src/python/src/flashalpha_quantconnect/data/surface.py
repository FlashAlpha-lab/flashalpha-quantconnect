"""Volatility-surface bar.

Mirrors ``flashalpha_historical.types.SurfaceResponse`` from
``GET /v1/surface/{symbol}?at=...``. The IV grid is exposed as a list of
lists (one row per tenor, one column per moneyness bucket) — same shape
the SDK returns.
"""

from __future__ import annotations

from typing import Any, List, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class SurfaceBar(PythonDataBase):
    """Volatility-surface bar.

    Mirrors ``flashalpha_historical.types.SurfaceResponse`` from
    ``GET /v1/surface/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(SurfaceBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_surface(algo, "SPY")``.

    The 2-D ``Iv`` grid lands as a list of lists — ``bar.Iv[tenor_idx][moneyness_idx]``.
    Tenors and moneyness buckets are aligned with ``bar.Tenors`` and
    ``bar.Moneyness`` respectively.
    """

    # Field defaults — populated by data.source.parse at Reader time.
    Ticker: str = ""
    Spot: Optional[float] = None
    AsOf: str = ""
    GridSize: Optional[int] = None
    Tenors: Optional[List[float]] = None
    Moneyness: Optional[List[float]] = None
    Iv: Optional[List[List[float]]] = None
    SlicesUsed: Optional[List[str]] = None

    # Field-name aliases: bar property -> JSON snake_case key.
    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("surface", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(SurfaceBar, line, config.Symbol, date)
