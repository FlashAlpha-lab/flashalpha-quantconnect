"""Narrative (verbal/LLM-friendly dealer-positioning summary) bar.

Mirrors ``flashalpha_historical.types.NarrativeResponse`` from
``GET /v1/exposure/narrative/{symbol}?at=...``. The ``Narrative`` block
carries hand-tuned, numbers-aware prose lines (regime, gex change, key
levels, flow, vanna, charm, 0DTE, outlook) plus a ``data`` sub-block with
the raw numbers backing the sentences. Both nested layers land on the
bar as plain ``dict`` values.

Plan-canonical endpoint slug is ``narrative``; SDK REST path is
``exposure/narrative``. Both resolve to the same SDK method.
"""

from __future__ import annotations

from typing import Any, Dict, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class NarrativeBar(PythonDataBase):
    """Narrative bar.

    Mirrors ``flashalpha_historical.types.NarrativeResponse`` from
    ``GET /v1/exposure/narrative/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(NarrativeBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_narrative(algo, "SPY")``.

    The ``Narrative`` block exposes prose lines under keys ``regime``,
    ``gex_change``, ``key_levels``, ``flow``, ``vanna``, ``charm``,
    ``zero_dte``, ``outlook`` plus a ``data`` sub-block carrying the raw
    numbers (``net_gex``, ``gamma_flip``, ``call_wall``, ``put_wall``,
    ``zero_dte_pct``, etc.).
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    Narrative: Optional[Dict[str, Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("narrative", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(NarrativeBar, line, config.Symbol, date)
