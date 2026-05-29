"""Integration tests for OptionQuoteBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path directly
against the real API. The option_quote bar is the only one in the
catalog whose upstream JSON root is an ARRAY (one row per contract), so
the bar overrides ``Reader`` to bypass the object-only parse helper.
Skips without FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import OptionQuoteBar, config
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
def test_option_quote_bar_fetch_and_parse_populates_quotes():
    """Layer 1: the overridden Reader hydrates ``Quotes`` from the array root."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("option/quote", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = OptionQuoteBar()
    bar = bar.Reader(SimpleNamespace(Symbol=symbol), line, TEST_DATE, False)

    assert bar is not None
    assert bar.Quotes is not None
    assert len(bar.Quotes) > 10
    first = bar.Quotes[0]
    assert "strike" in first
    assert first.get("type") in ("C", "P")


@pytest.mark.integration
def test_option_quote_bar_fields_match_rest_response():
    """Layer 2: the parsed list matches the raw SDK response row-for-row (length + first row)."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.option_quote(TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("option/quote", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = OptionQuoteBar().Reader(SimpleNamespace(Symbol=symbol), line, TEST_DATE, False)

    assert bar is not None
    assert isinstance(raw, list)
    assert len(bar.Quotes) == len(raw)
    raw_first = raw[0]
    bar_first = bar.Quotes[0]
    assert bar_first["strike"] == raw_first["strike"]
    assert bar_first["type"] == raw_first["type"]
    assert bar_first.get("expiry") == raw_first.get("expiry")
    assert bar_first.get("delta") == raw_first.get("delta")


def test_add_flashalpha_option_quote_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_option_quote
    from flashalpha_quantconnect.data.option_quote import (
        OptionQuoteBar as DirectOptionQuoteBar,
    )
    assert (
        add_flashalpha_option_quote.__module__
        == "flashalpha_quantconnect.extensions"
    )
    assert DirectOptionQuoteBar is not None
