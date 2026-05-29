"""Exposure bars: GEX, DEX, VEX, CHEX, ExposureSummary, ExposureLevels.

Each class mirrors the matching SDK response type from
``flashalpha_historical.types``. JSON field names are mapped onto bar
properties by ``data.source.parse`` — snake_case JSON keys auto-map to
PascalCase property names, and explicit overrides go through the
``_field_aliases`` class attribute (Python equivalent of C#'s
``[JsonPropertyName]``).

NOTE: every SDK response carries a ``symbol`` field, but QC LEAN's
``PythonData`` already exposes ``self.Symbol`` for the subscription
symbol. We expose the API's ``symbol`` value as ``Ticker`` instead —
mirrors the C# bar classes.

Nested response blocks (e.g. ExposureSummary's ``exposures`` /
``interpretation`` / ``hedging_estimate`` / ``zero_dte``, or
ExposureLevels' ``levels``) are exposed as raw ``dict`` values on the
bar — Python users drill in with subscript access. This is the same
shape returned by the underlying SDK (``flashalpha_historical`` uses
``TypedDict`` aliases that resolve to plain dicts at runtime).
"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

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


class DexBar(PythonDataBase):
    """Delta exposure (DEX) bar.

    Mirrors ``flashalpha_historical.types.DexResponse`` from
    ``GET /v1/exposure/dex/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(DexBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_dex(algo, "SPY")``.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    NetDex: Optional[float] = None
    Strikes: Optional[List[Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/dex", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(DexBar, line, config.Symbol, date)


class VexBar(PythonDataBase):
    """Vanna exposure (VEX) bar.

    Mirrors ``flashalpha_historical.types.VexResponse`` from
    ``GET /v1/exposure/vex/{symbol}?at=...``. Includes a textual
    ``VexInterpretation`` describing the vanna regime.

    Subscribe with: ``algo.AddData(VexBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_vex(algo, "SPY")``.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    NetVex: Optional[float] = None
    VexInterpretation: str = ""
    Strikes: Optional[List[Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/vex", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(VexBar, line, config.Symbol, date)


class ChexBar(PythonDataBase):
    """Charm exposure (CHEX) bar.

    Mirrors ``flashalpha_historical.types.ChexResponse`` from
    ``GET /v1/exposure/chex/{symbol}?at=...``. Includes a textual
    ``ChexInterpretation`` describing the charm regime.

    Subscribe with: ``algo.AddData(ChexBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_chex(algo, "SPY")``.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    NetChex: Optional[float] = None
    ChexInterpretation: str = ""
    Strikes: Optional[List[Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/chex", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(ChexBar, line, config.Symbol, date)


class ExposureSummaryBar(PythonDataBase):
    """Full exposure-summary bar.

    Mirrors ``flashalpha_historical.types.ExposureSummaryResponse`` from
    ``GET /v1/exposure/summary/{symbol}?at=...``.

    Nested blocks (``Exposures``, ``Interpretation``, ``HedgingEstimate``,
    ``ZeroDte``) are exposed as raw ``dict`` values — same shape the SDK
    returns. Drill in with ``bar.Exposures["net_gex"]`` etc.

    Subscribe with: ``algo.AddData(ExposureSummaryBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_exposure_summary(algo, "SPY")``.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    GammaFlip: Optional[float] = None
    Regime: str = ""
    Exposures: Optional[Dict[str, Any]] = None
    Interpretation: Optional[Dict[str, Any]] = None
    HedgingEstimate: Optional[Dict[str, Any]] = None
    ZeroDte: Optional[Dict[str, Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/summary", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(ExposureSummaryBar, line, config.Symbol, date)


class ExposureLevelsBar(PythonDataBase):
    """Exposure-levels bar — distilled dealer-flow key levels.

    Mirrors ``flashalpha_historical.types.ExposureLevelsResponse`` from
    ``GET /v1/exposure/levels/{symbol}?at=...``.

    The nested ``Levels`` block is exposed as a raw ``dict`` — drill in
    with ``bar.Levels["gamma_flip"]``, ``bar.Levels["call_wall"]``, etc.

    Subscribe with: ``algo.AddData(ExposureLevelsBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_exposure_levels(algo, "SPY")``.
    """

    Ticker: str = ""
    UnderlyingPrice: Optional[float] = None
    AsOf: str = ""
    Levels: Optional[Dict[str, Any]] = None

    _field_aliases = {
        "Ticker": "symbol",
    }

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/levels", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(ExposureLevelsBar, line, config.Symbol, date)
