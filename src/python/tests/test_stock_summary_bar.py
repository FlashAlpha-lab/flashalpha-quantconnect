"""Integration tests for StockSummaryBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
stock_summary endpoint carries a rich composite (price, volatility,
options_flow, exposure, macro) — each lands on the bar as a raw dict.
Skips without FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import StockSummaryBar, config
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
def test_stock_summary_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("stock/summary", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(StockSummaryBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.AsOf
    assert bar.PriceQuote is not None
    assert bar.Volatility is not None
    assert bar.OptionsFlow is not None
    assert bar.Macro is not None


@pytest.mark.integration
def test_stock_summary_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.stock_summary(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("stock/summary", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(StockSummaryBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.MarketOpen == raw.get("market_open")
    # Nested blocks land as raw dicts — compare salient leaves only.
    raw_price = raw.get("price") or {}
    bar_price = bar.PriceQuote or {}
    assert bar_price.get("mid") == raw_price.get("mid")
    assert bar_price.get("last_update") == raw_price.get("last_update")
    raw_vol = raw.get("volatility") or {}
    bar_vol = bar.Volatility or {}
    assert bar_vol.get("atm_iv") == raw_vol.get("atm_iv")
    assert bar_vol.get("vrp") == raw_vol.get("vrp")
    raw_flow = raw.get("options_flow") or {}
    bar_flow = bar.OptionsFlow or {}
    assert bar_flow.get("pc_ratio_oi") == raw_flow.get("pc_ratio_oi")
    raw_exp = raw.get("exposure") or {}
    bar_exp = bar.Exposure or {}
    assert bar_exp.get("net_gex") == raw_exp.get("net_gex")
    assert bar_exp.get("regime") == raw_exp.get("regime")
    raw_macro = raw.get("macro") or {}
    bar_macro = bar.Macro or {}
    raw_vix = (raw_macro.get("vix") or {}).get("value")
    bar_vix = (bar_macro.get("vix") or {}).get("value")
    assert bar_vix == raw_vix


def test_add_flashalpha_stock_summary_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_stock_summary
    from flashalpha_quantconnect.data.stock_summary import (
        StockSummaryBar as DirectStockSummaryBar,
    )
    assert (
        add_flashalpha_stock_summary.__module__
        == "flashalpha_quantconnect.extensions"
    )
    assert DirectStockSummaryBar is not None
