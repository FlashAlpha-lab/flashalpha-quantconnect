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
    add_flashalpha_vex,
)

__all__ = [
    "config",
    "GexBar",
    "DexBar",
    "VexBar",
    "ChexBar",
    "ExposureSummaryBar",
    "ExposureLevelsBar",
    "add_flashalpha_gex",
    "add_flashalpha_dex",
    "add_flashalpha_vex",
    "add_flashalpha_chex",
    "add_flashalpha_exposure_summary",
    "add_flashalpha_exposure_levels",
    "FlashAlphaAuthMissingException",
    "FlashAlphaNetworkException",
    "FlashAlphaQuantConnectException",
    "FlashAlphaRateLimitedException",
    "FlashAlphaUnauthorizedException",
]
