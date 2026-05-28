"""FlashAlphaHttpClient — the bridge's transport layer.

Mirrors the C# ``FlashAlpha.QuantConnect.Client.FlashAlphaHttpClient`` exactly:
switches on endpoint slug, calls the matching ``flashalpha_historical``
SDK method, returns the parsed JSON ``dict`` (the Python SDK already calls
``resp.json()`` internally), and translates a small set of SDK exceptions
to bridge exceptions.

Slug aliases
------------
For each REST path that differs between the plan's canonical form and the
SDK's actual URL, both forms are accepted — see the C# wrapper for the
authoritative list. Examples: ``max-pain`` and ``maxpain``;
``narrative`` and ``exposure/narrative``; ``stock/quote`` and
``stockquote``; ``adv-volatility``, ``adv_volatility``.

Exception translation
---------------------
Only three SDK exceptions are translated:

* ``AuthenticationError``  → ``FlashAlphaUnauthorizedException``
* ``RateLimitError``       → ``FlashAlphaRateLimitedException``
* ``requests`` network/timeout exceptions → ``FlashAlphaNetworkException``

Everything else (``NoDataError``, ``SymbolNotFoundError``,
``TierRestrictedError``, ``InsufficientDataError``, ``InvalidAtError``,
``NoCoverageError``, ``ServerError`` …) propagates unchanged so that
``Bar.Reader`` layers can handle the domain-specific conditions distinctly.
"""

from __future__ import annotations

from datetime import datetime
from typing import Any, Optional

import requests

from flashalpha_historical import (
    AuthenticationError,
    FlashAlphaHistorical,
    RateLimitError,
)

from . import config
from .exceptions import (
    FlashAlphaNetworkException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)


def _tail_of(key: str) -> str:
    return key[-4:] if len(key) >= 4 else "????"


def _format_at(at: datetime) -> str:
    """Match the C# wrapper: format datetimes as ``yyyy-MM-ddTHH:mm:ss``.

    The SDK accepts ``datetime`` directly but we pre-format here for
    forward-compatibility (SDK versions vary on which methods have
    datetime overloads vs. string-only)."""
    return at.strftime("%Y-%m-%dT%H:%M:%S")


class FlashAlphaHttpClient:
    """Synchronous bridge transport — mirrors the C# wrapper.

    Parameters
    ----------
    sdk : FlashAlphaHistorical, optional
        Pre-built SDK client to wrap. Used by tests and by callers that want
        to share a session. If omitted, the API key is resolved via
        :func:`flashalpha_quantconnect.config.resolve_api_key` and a new SDK
        client is built from ``config.base_url`` / ``config.http_timeout_s``.
    """

    def __init__(self, sdk: Optional[FlashAlphaHistorical] = None) -> None:
        if sdk is not None:
            self._sdk = sdk
            # Injected SDK — we don't know the key, so use the same
            # placeholder the C# side uses.
            self._key_tail4 = "????"
        else:
            key = config.resolve_api_key()
            self._sdk = FlashAlphaHistorical(
                api_key=key,
                base_url=config.base_url,
                timeout=config.http_timeout_s,
            )
            self._key_tail4 = _tail_of(key)

    def fetch_json(
        self,
        *,
        endpoint: str,
        ticker: Optional[str],
        at: datetime,
    ) -> dict[str, Any]:
        """Fetch a JSON payload for the given endpoint slug.

        Parameters
        ----------
        endpoint : str
            One of the slugs in the dispatch table below. Accepts both
            plan-canonical and SDK REST-path aliases (see module docstring).
        ticker : str
            Symbol, e.g. ``"SPY"``. Required for every endpoint except
            ``tickers``.
        at : datetime
            As-of timestamp; formatted as ``yyyy-MM-ddTHH:mm:ss``.

        Returns
        -------
        dict
            The parsed JSON response.

        Raises
        ------
        FlashAlphaUnauthorizedException
            On SDK ``AuthenticationError``.
        FlashAlphaRateLimitedException
            On SDK ``RateLimitError``.
        FlashAlphaNetworkException
            On ``requests`` connection/timeout errors.
        ValueError
            When the endpoint slug is not in the dispatch table.
        """
        if endpoint is None:
            raise ValueError("endpoint is required")
        if ticker is None and endpoint != "tickers":
            raise ValueError(f"ticker is required for endpoint '{endpoint}'")

        at_str = _format_at(at)
        sdk = self._sdk

        try:
            # Endpoint dispatch — keep order/grouping aligned with the C#
            # switch in src/csharp/.../FlashAlphaHttpClient.cs so future
            # additions only need to be made in two parallel places.
            if endpoint == "tickers":
                return sdk.tickers(symbol=None)

            # Market data — plan-canonical "stock/quote" / "option/quote"
            # plus SDK REST-path "stockquote" / "optionquote".
            if endpoint in ("stock/quote", "stockquote"):
                return sdk.stock_quote(ticker, at=at_str)
            if endpoint in ("option/quote", "optionquote"):
                return sdk.option_quote(ticker, at=at_str)
            if endpoint == "surface":
                return sdk.surface(ticker, at=at_str)

            # Exposure
            if endpoint == "exposure/gex":
                return sdk.gex(ticker, at=at_str)
            if endpoint == "exposure/dex":
                return sdk.dex(ticker, at=at_str)
            if endpoint == "exposure/vex":
                return sdk.vex(ticker, at=at_str)
            if endpoint == "exposure/chex":
                return sdk.chex(ticker, at=at_str)
            if endpoint == "exposure/summary":
                return sdk.exposure_summary(ticker, at=at_str)
            if endpoint == "exposure/levels":
                return sdk.exposure_levels(ticker, at=at_str)

            # Narrative — plan-canonical "narrative" + SDK "exposure/narrative"
            if endpoint in ("narrative", "exposure/narrative"):
                return sdk.narrative(ticker, at=at_str)

            if endpoint == "exposure/zero-dte":
                return sdk.zero_dte(ticker, at=at_str)

            # Max pain — plan-canonical "max-pain" + SDK "maxpain"
            if endpoint in ("max-pain", "maxpain"):
                return sdk.max_pain(ticker, at=at_str)

            # Composite
            if endpoint == "stock/summary":
                return sdk.stock_summary(ticker, at=at_str)

            # Volatility — plan-canonical "adv-volatility" + SDK "adv_volatility"
            if endpoint == "volatility":
                return sdk.volatility(ticker, at=at_str)
            if endpoint in ("adv-volatility", "adv_volatility"):
                return sdk.adv_volatility(ticker, at=at_str)

            # VRP
            if endpoint == "vrp":
                return sdk.vrp(ticker, at=at_str)

            raise ValueError(f"Unknown FlashAlpha endpoint slug: '{endpoint}'.")

        except AuthenticationError as err:
            raise FlashAlphaUnauthorizedException(endpoint, self._key_tail4) from err
        except RateLimitError as err:
            raise FlashAlphaRateLimitedException(endpoint) from err
        except (
            requests.exceptions.ConnectionError,
            requests.exceptions.Timeout,
            requests.exceptions.RequestException,
        ) as err:
            raise FlashAlphaNetworkException(endpoint) from err
