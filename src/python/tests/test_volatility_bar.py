"""Integration tests for VolatilityBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
volatility endpoint has rich nested blocks (realized_vol, iv_rv_spreads,
term_structure, iv_dispersion, put_call_profile, oi_concentration,
liquidity) plus per-row lists (skew_profiles, gex_by_dte, theta_by_dte,
hedging_scenarios) — those land on the bar as raw dicts / lists. Skips
without FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import VolatilityBar, config
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
def test_volatility_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("volatility", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(VolatilityBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.AtmIv is not None
    assert bar.RealizedVol is not None
    assert bar.IvRvSpreads is not None
    assert bar.TermStructure is not None


@pytest.mark.integration
def test_volatility_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.volatility(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("volatility", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(VolatilityBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.MarketOpen == raw.get("market_open")
    assert bar.AtmIv == raw.get("atm_iv")
    # Nested blocks land as raw dicts — compare salient leaves only.
    raw_rv = raw.get("realized_vol") or {}
    bar_rv = bar.RealizedVol or {}
    assert bar_rv.get("rv_20d") == raw_rv.get("rv_20d")
    raw_iv_rv = raw.get("iv_rv_spreads") or {}
    bar_iv_rv = bar.IvRvSpreads or {}
    assert bar_iv_rv.get("vrp_20d") == raw_iv_rv.get("vrp_20d")
    raw_term = raw.get("term_structure") or {}
    bar_term = bar.TermStructure or {}
    assert bar_term.get("state") == raw_term.get("state")
    raw_liq = raw.get("liquidity") or {}
    bar_liq = bar.Liquidity or {}
    assert bar_liq.get("atm_avg_spread_pct") == raw_liq.get("atm_avg_spread_pct")
    assert len(bar.SkewProfiles or []) == len(raw.get("skew_profiles") or [])
    assert len(bar.GexByDte or []) == len(raw.get("gex_by_dte") or [])
    assert len(bar.ThetaByDte or []) == len(raw.get("theta_by_dte") or [])
    assert len(bar.HedgingScenarios or []) == len(raw.get("hedging_scenarios") or [])


def test_add_flashalpha_volatility_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_volatility
    from flashalpha_quantconnect.data.volatility import VolatilityBar as DirectVolatilityBar
    assert add_flashalpha_volatility.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectVolatilityBar is not None
