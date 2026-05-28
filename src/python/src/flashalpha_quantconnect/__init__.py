"""FlashAlpha options-flow data as QuantConnect LEAN custom-data bars."""

__version__ = "0.1.0"

from . import config
from .exceptions import (
    FlashAlphaAuthMissingException,
    FlashAlphaNetworkException,
    FlashAlphaQuantConnectException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)

__all__ = [
    "config",
    "FlashAlphaAuthMissingException",
    "FlashAlphaNetworkException",
    "FlashAlphaQuantConnectException",
    "FlashAlphaRateLimitedException",
    "FlashAlphaUnauthorizedException",
]
