"""Integration tests for ZeroDteBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
zero-DTE endpoint has rich nested blocks (regime, exposures,
expected_move, pin_risk, hedging, decay, vol_context, flow, levels,
liquidity, metadata) — those land on the bar as raw dicts. On names
without a same-day expiry, the SDK returns a thin response. Skips
without FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import ZeroDteBar, config
from flashalpha_quantconnect.data import source


TEST_DATE = datetime(2024, 6, 14, 15, 30, 0)
TEST_TICKER = "SPY"


@pytest.fixture(autouse=True)
def _reset_source_cache():
    source.reset()
    yield
    source.reset()


def _make_symbol(ticker: str):
    return SimpleNamespace(Value=ticker)


@pytest.mark.integration
def test_zero_dte_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/zero-dte", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(ZeroDteBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    # On a no-zero-DTE response, nested blocks may be absent. Otherwise
    # the rich payload populates.
    if not bar.NoZeroDte:
        assert bar.Regime is not None
        assert bar.Exposures is not None
        assert bar.ExpectedMove is not None
        assert bar.Strikes is not None


@pytest.mark.integration
def test_zero_dte_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.zero_dte(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/zero-dte", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(ZeroDteBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.Expiration == raw.get("expiration", "")
    assert bar.MarketOpen == raw.get("market_open")
    assert bar.NoZeroDte == raw.get("no_zero_dte")
    # Nested blocks land as raw dicts — compare salient leaves only.
    raw_regime = raw.get("regime") or {}
    bar_regime = bar.Regime or {}
    assert bar_regime.get("label") == raw_regime.get("label")
    raw_exposures = raw.get("exposures") or {}
    bar_exposures = bar.Exposures or {}
    assert bar_exposures.get("net_gex") == raw_exposures.get("net_gex")
    raw_levels = raw.get("levels") or {}
    bar_levels = bar.Levels or {}
    assert bar_levels.get("call_wall") == raw_levels.get("call_wall")
    raw_strikes = raw.get("strikes") or []
    bar_strikes = bar.Strikes or []
    assert len(bar_strikes) == len(raw_strikes)


def test_add_flashalpha_zero_dte_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_zero_dte
    from flashalpha_quantconnect.data.zero_dte import ZeroDteBar as DirectZeroDteBar
    assert add_flashalpha_zero_dte.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectZeroDteBar is not None
