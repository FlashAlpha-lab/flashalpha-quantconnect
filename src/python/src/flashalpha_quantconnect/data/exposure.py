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


# Lazy import — PythonData only resolves inside LEAN. Tests import the bar
# class via this module but exercise GetSource/Reader by calling the shared
# helpers directly, so the LEAN base class is not load-bearing for tests.
#
# IMPORTANT: ``quantconnect-stubs``' ``QuantConnect/__init__.py`` mutates
# ``sys.path`` (removes site-packages, calls ``del sys.modules["QuantConnect"]``,
# then attempts ``from clr import AddReference``) — when pythonnet isn't
# installed (i.e. outside the LEAN runtime), the ``clr`` import raises but
# ``sys.path`` is never restored, which then breaks every subsequent
# third-party import in the process. We snapshot/restore ``sys.path`` and
# the ``QuantConnect`` ``sys.modules`` entry around the try so the failure
# is fully contained.
import sys as _sys
_saved_sys_path = _sys.path[:]
_saved_qc_module = _sys.modules.get("QuantConnect")
try:
    from QuantConnect.Python import PythonData as _PythonDataBase
except Exception:
    # Stand-in base so the module imports cleanly outside LEAN.
    class _PythonDataBase:  # type: ignore
        pass
    # Restore the path / module state the stubs init mutated before failing.
    _sys.path = _saved_sys_path
    if _saved_qc_module is not None:
        _sys.modules["QuantConnect"] = _saved_qc_module
    elif "QuantConnect" in _sys.modules and _sys.modules["QuantConnect"] is None:
        _sys.modules.pop("QuantConnect", None)
del _sys, _saved_sys_path, _saved_qc_module


from .source import source_for, parse


class GexBar(_PythonDataBase):
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
