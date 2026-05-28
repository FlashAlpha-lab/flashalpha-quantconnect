"""Module-level configuration for the bridge.

Resolution order for the FlashAlpha API key:
  1. Explicit override (set config.api_key = "...")
  2. QC GetParameter("flashalpha-api-key") for QC Cloud
  3. Environment variable FLASHALPHA_API_KEY
"""

from __future__ import annotations

import os
from typing import Callable, Optional

from .exceptions import FlashAlphaAuthMissingException


api_key: Optional[str] = None
base_url: str = "https://historical.flashalpha.com"
http_timeout_s: float = 30.0
max_retries: int = 3


def resolve_api_key(
    qc_get_parameter: Optional[Callable[[str], Optional[str]]] = None,
) -> str:
    """Walk the resolution order; raise FlashAlphaAuthMissingException if all miss."""
    if api_key:
        return api_key

    if qc_get_parameter is not None:
        from_qc = qc_get_parameter("flashalpha-api-key")
        if from_qc:
            return from_qc

    from_env = os.environ.get("FLASHALPHA_API_KEY")
    if from_env:
        return from_env

    raise FlashAlphaAuthMissingException()


def reset() -> None:
    """Reset module state — for tests only."""
    global api_key, base_url, http_timeout_s, max_retries
    api_key = None
    base_url = "https://historical.flashalpha.com"
    http_timeout_s = 30.0
    max_retries = 3
