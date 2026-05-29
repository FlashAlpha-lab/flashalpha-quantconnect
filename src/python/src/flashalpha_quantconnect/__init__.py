"""FlashAlpha options-flow data as QuantConnect LEAN custom-data bars."""

__version__ = "0.1.0"

from . import config
from .data.exposure import (
    ChexBar,
    DexBar,
    ExposureLevelsBar,
    ExposureSummaryBar,
    GexBar,
    VexBar,
)
from .data.max_pain import MaxPainBar
from .data.surface import SurfaceBar
from .data.zero_dte import ZeroDteBar
from .exceptions import (
    FlashAlphaAuthMissingException,
    FlashAlphaNetworkException,
    FlashAlphaQuantConnectException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)
from .extensions import (
    add_flashalpha_chex,
    add_flashalpha_dex,
    add_flashalpha_exposure_levels,
    add_flashalpha_exposure_summary,
    add_flashalpha_gex,
    add_flashalpha_max_pain,
    add_flashalpha_surface,
    add_flashalpha_vex,
    add_flashalpha_zero_dte,
)

__all__ = [
    "config",
    "GexBar",
    "DexBar",
    "VexBar",
    "ChexBar",
    "ExposureSummaryBar",
    "ExposureLevelsBar",
    "SurfaceBar",
    "ZeroDteBar",
    "MaxPainBar",
    "add_flashalpha_gex",
    "add_flashalpha_dex",
    "add_flashalpha_vex",
    "add_flashalpha_chex",
    "add_flashalpha_exposure_summary",
    "add_flashalpha_exposure_levels",
    "add_flashalpha_surface",
    "add_flashalpha_zero_dte",
    "add_flashalpha_max_pain",
    "FlashAlphaAuthMissingException",
    "FlashAlphaNetworkException",
    "FlashAlphaQuantConnectException",
    "FlashAlphaRateLimitedException",
    "FlashAlphaUnauthorizedException",
]
