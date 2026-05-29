"""FlashAlpha options-flow data as QuantConnect LEAN custom-data bars."""

__version__ = "0.1.0"

from . import config
from .data.exposure import GexBar
from .exceptions import (
    FlashAlphaAuthMissingException,
    FlashAlphaNetworkException,
    FlashAlphaQuantConnectException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)
from .extensions import add_flashalpha_gex

__all__ = [
    "config",
    "GexBar",
    "add_flashalpha_gex",
    "FlashAlphaAuthMissingException",
    "FlashAlphaNetworkException",
    "FlashAlphaQuantConnectException",
    "FlashAlphaRateLimitedException",
    "FlashAlphaUnauthorizedException",
]
