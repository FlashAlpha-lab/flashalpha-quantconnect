"""Exception hierarchy for flashalpha-quantconnect.

All bridge exceptions inherit from FlashAlphaQuantConnectException so users
can catch the family in one block. Each exception has a fixed error code
that maps to docs/troubleshooting.md.
"""


class FlashAlphaQuantConnectException(Exception):
    """Base for all bridge exceptions."""

    error_code: str = "FA-000"

    @property
    def doc_url(self) -> str:
        return (
            "https://github.com/FlashAlpha-lab/flashalpha-quantconnect/"
            f"blob/main/docs/troubleshooting.md#{self.error_code.lower()}"
        )


class FlashAlphaAuthMissingException(FlashAlphaQuantConnectException):
    error_code = "FA-AUTH-001"

    def __init__(self) -> None:
        super().__init__(
            "FlashAlpha API key not found. Set FLASHALPHA_API_KEY env var, "
            "flashalpha_quantconnect.config.api_key, or QC parameter "
            "'flashalpha-api-key'.",
        )


class FlashAlphaUnauthorizedException(FlashAlphaQuantConnectException):
    error_code = "FA-AUTH-002"

    def __init__(self, endpoint: str, key_tail4: str) -> None:
        super().__init__(
            f"FlashAlpha rejected the API key (…{key_tail4}) on endpoint {endpoint}.",
        )


class FlashAlphaRateLimitedException(FlashAlphaQuantConnectException):
    error_code = "FA-RATE-001"

    def __init__(self, endpoint: str) -> None:
        super().__init__(
            f"FlashAlpha rate-limited the request to {endpoint} after retries exhausted.",
        )


class FlashAlphaNetworkException(FlashAlphaQuantConnectException):
    error_code = "FA-NET-001"

    def __init__(self, endpoint: str) -> None:
        super().__init__(f"Network error talking to FlashAlpha at {endpoint}.")
