"""Tests for FlashAlphaHttpClient.

Includes unit-level tests with a stub SDK (no network) and one integration
test gated by FLASHALPHA_API_KEY via the conftest hook.
"""

from __future__ import annotations

from datetime import datetime
from typing import Any

import pytest

from flashalpha_historical import (
    AuthenticationError,
    RateLimitError,
)

from flashalpha_quantconnect import config
from flashalpha_quantconnect.client import FlashAlphaHttpClient
from flashalpha_quantconnect.exceptions import (
    FlashAlphaNetworkException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)


# ── Stub SDK ────────────────────────────────────────────────────────


class StubSdk:
    """Records every call and returns a canned payload (or raises an exc)."""

    def __init__(self, payload: dict | None = None, raises: Exception | None = None):
        self._payload = payload if payload is not None else {"ok": True}
        self._raises = raises
        self.calls: list[tuple[str, tuple, dict]] = []

    def _record(self, name: str, args: tuple, kwargs: dict) -> Any:
        self.calls.append((name, args, kwargs))
        if self._raises is not None:
            raise self._raises
        return self._payload

    def tickers(self, *args, **kwargs):
        return self._record("tickers", args, kwargs)

    def stock_quote(self, *args, **kwargs):
        return self._record("stock_quote", args, kwargs)

    def option_quote(self, *args, **kwargs):
        return self._record("option_quote", args, kwargs)

    def surface(self, *args, **kwargs):
        return self._record("surface", args, kwargs)

    def gex(self, *args, **kwargs):
        return self._record("gex", args, kwargs)

    def dex(self, *args, **kwargs):
        return self._record("dex", args, kwargs)

    def vex(self, *args, **kwargs):
        return self._record("vex", args, kwargs)

    def chex(self, *args, **kwargs):
        return self._record("chex", args, kwargs)

    def exposure_summary(self, *args, **kwargs):
        return self._record("exposure_summary", args, kwargs)

    def exposure_levels(self, *args, **kwargs):
        return self._record("exposure_levels", args, kwargs)

    def narrative(self, *args, **kwargs):
        return self._record("narrative", args, kwargs)

    def zero_dte(self, *args, **kwargs):
        return self._record("zero_dte", args, kwargs)

    def max_pain(self, *args, **kwargs):
        return self._record("max_pain", args, kwargs)

    def stock_summary(self, *args, **kwargs):
        return self._record("stock_summary", args, kwargs)

    def volatility(self, *args, **kwargs):
        return self._record("volatility", args, kwargs)

    def adv_volatility(self, *args, **kwargs):
        return self._record("adv_volatility", args, kwargs)

    def vrp(self, *args, **kwargs):
        return self._record("vrp", args, kwargs)


AT = datetime(2024, 6, 14, 15, 30, 0)
EXPECTED_AT_STR = "2024-06-14T15:30:00"


# ── Endpoint slug coverage ──────────────────────────────────────────


@pytest.mark.parametrize(
    ("endpoint", "expected_method"),
    [
        # tickers: no ticker arg
        ("tickers", "tickers"),
        # market data — plan-canonical + SDK REST-path aliases
        ("stock/quote", "stock_quote"),
        ("stockquote", "stock_quote"),
        ("option/quote", "option_quote"),
        ("optionquote", "option_quote"),
        ("surface", "surface"),
        # exposure
        ("exposure/gex", "gex"),
        ("exposure/dex", "dex"),
        ("exposure/vex", "vex"),
        ("exposure/chex", "chex"),
        ("exposure/summary", "exposure_summary"),
        ("exposure/levels", "exposure_levels"),
        # narrative — plan-canonical + SDK REST-path
        ("narrative", "narrative"),
        ("exposure/narrative", "narrative"),
        # zero-dte
        ("exposure/zero-dte", "zero_dte"),
        # max pain — plan-canonical + SDK REST-path
        ("max-pain", "max_pain"),
        ("maxpain", "max_pain"),
        # composite
        ("stock/summary", "stock_summary"),
        # volatility — plan-canonical + SDK REST-path
        ("volatility", "volatility"),
        ("adv-volatility", "adv_volatility"),
        ("adv_volatility", "adv_volatility"),
        # vrp
        ("vrp", "vrp"),
    ],
)
def test_endpoint_dispatches_to_sdk_method(endpoint: str, expected_method: str) -> None:
    stub = StubSdk(payload={"symbol": "SPY"})
    client = FlashAlphaHttpClient(sdk=stub)

    raw = client.fetch_json(endpoint=endpoint, ticker="SPY", at=AT)

    assert raw == {"symbol": "SPY"}
    assert len(stub.calls) == 1
    name, _args, _kwargs = stub.calls[0]
    assert name == expected_method


def test_at_is_formatted_as_iso() -> None:
    stub = StubSdk()
    client = FlashAlphaHttpClient(sdk=stub)

    client.fetch_json(endpoint="exposure/gex", ticker="SPY", at=AT)

    _name, args, kwargs = stub.calls[0]
    # `at` is passed as keyword to the SDK
    assert kwargs.get("at") == EXPECTED_AT_STR


def test_tickers_does_not_pass_ticker() -> None:
    stub = StubSdk(payload={"tickers": []})
    client = FlashAlphaHttpClient(sdk=stub)

    # ticker is irrelevant — SDK.tickers() takes no ticker positional
    client.fetch_json(endpoint="tickers", ticker="SPY", at=AT)

    _name, args, kwargs = stub.calls[0]
    assert args == ()
    # symbol kwarg is None (full coverage table)
    assert kwargs.get("symbol") is None


def test_unknown_endpoint_raises_value_error() -> None:
    stub = StubSdk()
    client = FlashAlphaHttpClient(sdk=stub)

    with pytest.raises(ValueError, match="Unknown FlashAlpha endpoint slug"):
        client.fetch_json(endpoint="bogus/path", ticker="SPY", at=AT)


# ── Exception translation ───────────────────────────────────────────


def test_authentication_error_translates_to_unauthorized() -> None:
    stub = StubSdk(raises=AuthenticationError("invalid key", status_code=401))
    client = FlashAlphaHttpClient(sdk=stub)

    with pytest.raises(FlashAlphaUnauthorizedException) as exc_info:
        client.fetch_json(endpoint="exposure/gex", ticker="SPY", at=AT)

    assert "exposure/gex" in str(exc_info.value)
    # Injected-SDK constructor uses placeholder tail
    assert "????" in str(exc_info.value)


def test_rate_limit_error_translates() -> None:
    stub = StubSdk(raises=RateLimitError("daily quota exhausted", status_code=429))
    client = FlashAlphaHttpClient(sdk=stub)

    with pytest.raises(FlashAlphaRateLimitedException) as exc_info:
        client.fetch_json(endpoint="vrp", ticker="SPY", at=AT)

    assert "vrp" in str(exc_info.value)


def test_network_error_translates() -> None:
    import requests

    stub = StubSdk(raises=requests.exceptions.ConnectionError("dns failure"))
    client = FlashAlphaHttpClient(sdk=stub)

    with pytest.raises(FlashAlphaNetworkException) as exc_info:
        client.fetch_json(endpoint="exposure/summary", ticker="SPY", at=AT)

    assert "exposure/summary" in str(exc_info.value)


def test_timeout_translates_to_network() -> None:
    import requests

    stub = StubSdk(raises=requests.exceptions.Timeout("read timeout"))
    client = FlashAlphaHttpClient(sdk=stub)

    with pytest.raises(FlashAlphaNetworkException):
        client.fetch_json(endpoint="stock/summary", ticker="SPY", at=AT)


def test_other_sdk_errors_propagate_unchanged() -> None:
    """NoDataError, SymbolNotFoundError, TierRestrictedError etc.
    are intentionally NOT translated — the bar.Reader layer handles them."""
    from flashalpha_historical import NoDataError

    stub = StubSdk(raises=NoDataError("no data at that timestamp", status_code=404))
    client = FlashAlphaHttpClient(sdk=stub)

    with pytest.raises(NoDataError):
        client.fetch_json(endpoint="exposure/gex", ticker="SPY", at=AT)


# ── Default constructor (resolves key from config) ──────────────────


def test_default_constructor_uses_resolved_key(monkeypatch: pytest.MonkeyPatch) -> None:
    """When no SDK is injected, the client resolves the key via config and
    builds a FlashAlphaHistorical SDK with it."""
    captured: dict[str, Any] = {}

    class FakeSdk:
        def __init__(self, api_key, *, base_url, timeout):
            captured["api_key"] = api_key
            captured["base_url"] = base_url
            captured["timeout"] = timeout

    import flashalpha_quantconnect.client as client_mod

    monkeypatch.setattr(client_mod, "FlashAlphaHistorical", FakeSdk)
    monkeypatch.setattr(config, "api_key", "test-key-abcd")

    try:
        c = FlashAlphaHttpClient()
        assert captured["api_key"] == "test-key-abcd"
        # last 4 of the key get tucked into the unauthorized exception
        assert c._key_tail4 == "abcd"
    finally:
        config.reset()


# ── Integration (live API) ──────────────────────────────────────────


@pytest.mark.integration
def test_fetch_gex_json_returns_payload() -> None:
    client = FlashAlphaHttpClient()
    raw = client.fetch_json(endpoint="exposure/gex", ticker="SPY", at=datetime(2024, 6, 14))
    assert isinstance(raw, dict)
    assert raw.get("symbol") == "SPY"
    assert "net_gex" in raw
