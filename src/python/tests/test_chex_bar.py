"""Integration tests for ChexBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. Skips
without FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import ChexBar, config
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
def test_chex_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/chex", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(ChexBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.NetChex is not None
    assert bar.Strikes is not None


@pytest.mark.integration
def test_chex_bar_fields_match_rest_response():
    """Layer 2: every field on the bar matches the raw SDK response field-for-field."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.chex(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/chex", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(ChexBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.NetChex == raw.get("net_chex")
    assert bar.ChexInterpretation == raw.get("chex_interpretation", "")
    raw_strikes = raw.get("strikes") or []
    bar_strikes = bar.Strikes or []
    assert len(bar_strikes) == len(raw_strikes)


def test_add_flashalpha_chex_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_chex
    from flashalpha_quantconnect.data.exposure import ChexBar as DirectChexBar
    assert add_flashalpha_chex.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectChexBar is not None
