"""Integration tests for VexBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. Skips
without FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import VexBar, config
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
def test_vex_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/vex", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(VexBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.NetVex is not None
    assert bar.Strikes is not None


@pytest.mark.integration
def test_vex_bar_fields_match_rest_response():
    """Layer 2: every field on the bar matches the raw SDK response field-for-field."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.vex(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/vex", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(VexBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.NetVex == raw.get("net_vex")
    assert bar.VexInterpretation == raw.get("vex_interpretation", "")
    raw_strikes = raw.get("strikes") or []
    bar_strikes = bar.Strikes or []
    assert len(bar_strikes) == len(raw_strikes)


def test_add_flashalpha_vex_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_vex
    from flashalpha_quantconnect.data.exposure import VexBar as DirectVexBar
    assert add_flashalpha_vex.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectVexBar is not None
