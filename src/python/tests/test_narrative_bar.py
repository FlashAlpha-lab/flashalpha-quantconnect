"""Integration tests for NarrativeBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
narrative endpoint carries a single nested ``narrative`` block with
hand-tuned prose lines plus a ``data`` sub-block of raw numbers — both
layers land on the bar as plain dicts. Skips without FLASHALPHA_API_KEY
(see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import NarrativeBar, config
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
def test_narrative_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("narrative", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(NarrativeBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.Narrative is not None
    assert bar.Narrative.get("data") is not None


@pytest.mark.integration
def test_narrative_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.narrative(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("narrative", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(NarrativeBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    # Narrative block — compare prose lines + salient data values.
    raw_narr = raw.get("narrative") or {}
    bar_narr = bar.Narrative or {}
    assert bar_narr.get("regime") == raw_narr.get("regime")
    assert bar_narr.get("gex_change") == raw_narr.get("gex_change")
    assert bar_narr.get("key_levels") == raw_narr.get("key_levels")
    assert bar_narr.get("outlook") == raw_narr.get("outlook")
    raw_data = raw_narr.get("data") or {}
    bar_data = bar_narr.get("data") or {}
    assert bar_data.get("net_gex") == raw_data.get("net_gex")
    assert bar_data.get("gamma_flip") == raw_data.get("gamma_flip")
    assert bar_data.get("call_wall") == raw_data.get("call_wall")
    assert bar_data.get("put_wall") == raw_data.get("put_wall")
    assert bar_data.get("regime") == raw_data.get("regime")
    assert len(bar_data.get("top_oi_changes") or []) == len(
        raw_data.get("top_oi_changes") or []
    )


def test_add_flashalpha_narrative_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_narrative
    from flashalpha_quantconnect.data.narrative import NarrativeBar as DirectNarrativeBar
    assert add_flashalpha_narrative.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectNarrativeBar is not None
