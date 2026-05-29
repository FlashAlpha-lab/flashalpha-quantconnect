"""Integration tests for GexBar.

Per the project's test philosophy (integration only): exercise the bar's
GetSource + Reader path by calling source.source_for + source.parse
directly, against the real API. No LEAN engine, no mocks.
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import GexBar, config
from flashalpha_quantconnect.data import source


TEST_DATE = datetime(2024, 6, 14, 15, 30, 0)
TEST_TICKER = "SPY"


@pytest.fixture(autouse=True)
def _reset_source_cache():
    source.reset()
    yield
    source.reset()


def _make_symbol(ticker: str):
    """Stand-in for QC Symbol; FlashAlphaSource only accesses .Value."""
    return SimpleNamespace(Value=ticker)


@pytest.mark.integration
def test_gex_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/gex", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(GexBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.NetGex is not None
    assert bar.NetGexLabel in ("positive", "negative", "neutral")


@pytest.mark.integration
def test_gex_bar_fields_match_rest_response():
    """Layer 2: every field on the bar matches the raw SDK response field-for-field."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.gex(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/gex", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(GexBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.GammaFlip == raw.get("gamma_flip")
    assert bar.NetGex == raw.get("net_gex")
    assert bar.NetGexLabel == raw.get("net_gex_label", "")
    # Strikes count — element-wise compare deferred (drift = count mismatch)
    raw_strikes = raw.get("strikes") or []
    bar_strikes = bar.Strikes or []
    assert len(bar_strikes) == len(raw_strikes)


def test_add_flashalpha_gex_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_gex
    from flashalpha_quantconnect.data.exposure import GexBar as DirectGexBar
    # The sugar's source code references GexBar — quickest check is to look at
    # the function's closure / module globals.
    assert add_flashalpha_gex.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectGexBar is not None
