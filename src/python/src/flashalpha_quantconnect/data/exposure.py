"""Exposure bars: GEX (and, later, DEX, VEX, CHEX, ExposureSummary, ExposureLevels).

Each class mirrors the matching SDK response type from
``flashalpha_historical.types``. JSON field names are mapped onto bar
properties by ``data.source.parse`` — snake_case JSON keys auto-map to
PascalCase property names, and explicit overrides go through the
``_field_aliases`` class attribute (Python equivalent of C#'s
``[JsonPropertyName]``).

NOTE: the SDK response carries a ``symbol`` field, but QC LEAN's
``PythonData`` already exposes ``self.Symbol`` for the subscription
symbol. We expose the API's ``symbol`` value as ``Ticker`` instead —
mirrors the C# ``FlashAlphaGexBar``.
"""

from __future__ import annotations

from typing import Any, List, Optional

from ._lean_import import PythonDataBase
from .source import source_for, parse


class GexBar(PythonDataBase):
    """Gamma exposure (GEX) bar.

    Mirrors ``flashalpha_historical.types.GexResponse`` from
    ``GET /v1/exposure/gex/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(GexBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_gex(algo, "SPY")``.
    """

    # Field defaults — populated by data.source.parse at Reader time.
    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    GammaFlip: Optional[float] = None
    NetGex: Optional[float] = None
    NetGexLabel: str = ""
    Strikes: Optional[List[Any]] = None

    # Field-name aliases: bar property -> JSON snake_case key.
    # Used by data.source.parse when the default PascalCase auto-mapping
    # doesn't apply (here: JSON ``symbol`` cannot map onto ``Symbol``
    # because QC's PythonData already owns that property).
    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/gex", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(GexBar, line, config.Symbol, date)
