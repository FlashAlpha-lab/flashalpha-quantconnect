"""Integration tests for ExposureSummaryBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
ExposureSummary endpoint has rich nested blocks (exposures,
interpretation, hedging_estimate, zero_dte) — those land on the bar as
raw dicts. Skips without FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import ExposureSummaryBar, config
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
def test_exposure_summary_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/summary", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(ExposureSummaryBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.Regime in ("positive_gamma", "negative_gamma", "unknown")
    assert bar.Exposures is not None
    assert bar.Interpretation is not None
    assert bar.HedgingEstimate is not None
    # ZeroDte may be absent on names without same-day expiry — don't enforce.


@pytest.mark.integration
def test_exposure_summary_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.exposure_summary(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("exposure/summary", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(ExposureSummaryBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.GammaFlip == raw.get("gamma_flip")
    assert bar.Regime == raw.get("regime", "")
    # Nested blocks land as raw dicts — compare salient leaves only.
    raw_exposures = raw.get("exposures") or {}
    bar_exposures = bar.Exposures or {}
    assert bar_exposures.get("net_gex") == raw_exposures.get("net_gex")
    assert bar_exposures.get("net_dex") == raw_exposures.get("net_dex")
    assert bar_exposures.get("net_vex") == raw_exposures.get("net_vex")
    assert bar_exposures.get("net_chex") == raw_exposures.get("net_chex")
    raw_interp = raw.get("interpretation") or {}
    bar_interp = bar.Interpretation or {}
    assert bar_interp.get("gamma") == raw_interp.get("gamma")


def test_add_flashalpha_exposure_summary_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_exposure_summary
    from flashalpha_quantconnect.data.exposure import (
        ExposureSummaryBar as DirectExposureSummaryBar,
    )
    assert add_flashalpha_exposure_summary.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectExposureSummaryBar is not None
