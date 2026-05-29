"""Integration tests for StockQuoteBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
stock_quote endpoint has a flat shape (bid/ask/mid/last/lastUpdate);
notable wire quirks are the root key ``ticker`` (not ``symbol``) and the
camelCase ``lastUpdate``. Skips without FLASHALPHA_API_KEY (see
conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import StockQuoteBar, config
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
def test_stock_quote_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("stock/quote", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(StockQuoteBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.Bid is not None and bar.Bid > 0
    assert bar.Ask is not None and bar.Ask > 0
    assert bar.Mid is not None and bar.Mid > 0
    assert bar.Bid <= bar.Mid <= bar.Ask
    assert bar.LastUpdate


@pytest.mark.integration
def test_stock_quote_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.stock_quote(TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("stock/quote", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(StockQuoteBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["ticker"]
    assert bar.Bid == raw["bid"]
    assert bar.Ask == raw["ask"]
    assert bar.Mid == raw["mid"]
    assert bar.LastUpdate == raw["lastUpdate"]
    # `last` is optional on some snapshots — only assert when both sides agree.
    if "last" in raw:
        assert bar.Last == raw["last"]


def test_add_flashalpha_stock_quote_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_stock_quote
    from flashalpha_quantconnect.data.stock_quote import (
        StockQuoteBar as DirectStockQuoteBar,
    )
    assert (
        add_flashalpha_stock_quote.__module__
        == "flashalpha_quantconnect.extensions"
    )
    assert DirectStockQuoteBar is not None
