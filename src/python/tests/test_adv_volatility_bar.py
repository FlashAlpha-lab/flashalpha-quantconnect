"""Integration tests for AdvVolatilityBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
adv-volatility endpoint carries per-expiry SVI parameter lists, forward
prices, the full total-variance surface, arbitrage flags, variance swap
fair values, and second-/third-order greek surfaces. Skips without
FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import AdvVolatilityBar, config
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
def test_adv_volatility_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("adv-volatility", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(AdvVolatilityBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.SviParameters is not None
    assert bar.ForwardPrices is not None
    assert bar.TotalVarianceSurface is not None
    assert bar.GreeksSurfaces is not None


@pytest.mark.integration
def test_adv_volatility_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.adv_volatility(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("adv-volatility", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(AdvVolatilityBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.MarketOpen == raw.get("market_open")
    # Per-expiry lists — lengths only; values are floats.
    assert len(bar.SviParameters or []) == len(raw.get("svi_parameters") or [])
    assert len(bar.ForwardPrices or []) == len(raw.get("forward_prices") or [])
    assert len(bar.ArbitrageFlags or []) == len(raw.get("arbitrage_flags") or [])
    assert len(bar.VarianceSwapFairValues or []) == len(
        raw.get("variance_swap_fair_values") or []
    )
    # Surface and greek grids — array lengths only.
    raw_surface = raw.get("total_variance_surface") or {}
    bar_surface = bar.TotalVarianceSurface or {}
    assert len(bar_surface.get("moneyness") or []) == len(
        raw_surface.get("moneyness") or []
    )
    assert len(bar_surface.get("expiries") or []) == len(
        raw_surface.get("expiries") or []
    )
    assert len(bar_surface.get("total_variance") or []) == len(
        raw_surface.get("total_variance") or []
    )
    raw_greeks = raw.get("greeks_surfaces") or {}
    bar_greeks = bar.GreeksSurfaces or {}
    raw_vanna = raw_greeks.get("vanna") or {}
    bar_vanna = bar_greeks.get("vanna") or {}
    assert len(bar_vanna.get("strikes") or []) == len(raw_vanna.get("strikes") or [])


def test_add_flashalpha_adv_volatility_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_adv_volatility
    from flashalpha_quantconnect.data.volatility import (
        AdvVolatilityBar as DirectAdvVolatilityBar,
    )
    assert add_flashalpha_adv_volatility.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectAdvVolatilityBar is not None
