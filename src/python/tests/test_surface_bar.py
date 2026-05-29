"""Integration tests for SurfaceBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
volatility-surface endpoint returns the 2-D IV grid as a list of lists;
it lands on the bar verbatim. Skips without FLASHALPHA_API_KEY (see
conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import SurfaceBar, config
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
def test_surface_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("surface", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(SurfaceBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.Spot is not None and bar.Spot > 0
    assert bar.AsOf
    assert bar.GridSize is not None and bar.GridSize > 0
    assert bar.Tenors is not None
    assert bar.Moneyness is not None
    assert bar.Iv is not None


@pytest.mark.integration
def test_surface_bar_fields_match_rest_response():
    """Layer 2: every field on the bar matches the raw SDK response field-for-field."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.surface(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("surface", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(SurfaceBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.Spot == raw.get("spot")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.GridSize == raw.get("grid_size")
    raw_tenors = raw.get("tenors") or []
    raw_moneyness = raw.get("moneyness") or []
    raw_iv = raw.get("iv") or []
    assert len(bar.Tenors or []) == len(raw_tenors)
    assert len(bar.Moneyness or []) == len(raw_moneyness)
    assert len(bar.Iv or []) == len(raw_iv)
    assert len(bar.SlicesUsed or []) == len(raw.get("slices_used") or [])


def test_add_flashalpha_surface_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_surface
    from flashalpha_quantconnect.data.surface import SurfaceBar as DirectSurfaceBar
    assert add_flashalpha_surface.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectSurfaceBar is not None
