"""Integration tests for VrpBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
VRP endpoint has nested blocks (vrp, directional, gex_conditioned,
vanna_conditioned, regime, strategy_scores, macro) plus a term-vrp list
and a warnings list — those land on the bar as raw dicts / lists. Skips
without FLASHALPHA_API_KEY (see conftest.py).

VRP returns 403 ``tier_restricted`` for anything below Alpha plan — that
gets surfaced as a network exception from the SDK at fetch time. If the
configured API key isn't Alpha, the test infrastructure (conftest skip
or exception handling) takes over; this file doesn't try to special-case
it.
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import VrpBar, config
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
def test_vrp_bar_fetch_and_parse_populates_fields():
    """Layer 1: subscribe path produces a bar with non-empty key fields."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("vrp", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(VrpBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == TEST_TICKER
    assert bar.UnderlyingPrice is not None and bar.UnderlyingPrice > 0
    assert bar.AsOf
    assert bar.Vrp is not None
    assert bar.Directional is not None
    assert bar.Regime is not None
    assert bar.Warnings is not None


@pytest.mark.integration
def test_vrp_bar_fields_match_rest_response():
    """Layer 2: every top-level field on the bar matches the raw SDK response."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    raw = sdk.vrp(symbol=TEST_TICKER, at=TEST_DATE)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("vrp", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(VrpBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Ticker == raw["symbol"]
    assert bar.UnderlyingPrice == raw.get("underlying_price")
    assert bar.AsOf == raw.get("as_of", "")
    assert bar.MarketOpen == raw.get("market_open")
    assert bar.VarianceRiskPremium == raw.get("variance_risk_premium")
    assert bar.ConvexityPremium == raw.get("convexity_premium")
    assert bar.FairVol == raw.get("fair_vol")
    assert bar.NetHarvestScore == raw.get("net_harvest_score")
    assert bar.DealerFlowRisk == raw.get("dealer_flow_risk")
    # Nested blocks land as raw dicts — silent-null traps documented on VrpBar.
    raw_vrp = raw.get("vrp") or {}
    bar_vrp = bar.Vrp or {}
    assert bar_vrp.get("vrp_20d") == raw_vrp.get("vrp_20d")
    assert bar_vrp.get("z_score") == raw_vrp.get("z_score")
    assert bar_vrp.get("percentile") == raw_vrp.get("percentile")
    raw_dir = raw.get("directional") or {}
    bar_dir = bar.Directional or {}
    assert bar_dir.get("downside_vrp") == raw_dir.get("downside_vrp")
    raw_regime = raw.get("regime") or {}
    bar_regime = bar.Regime or {}
    assert bar_regime.get("net_gex") == raw_regime.get("net_gex")
    assert bar_regime.get("vrp_regime") == raw_regime.get("vrp_regime")
    raw_gex_cond = raw.get("gex_conditioned") or {}
    bar_gex_cond = bar.GexConditioned or {}
    assert bar_gex_cond.get("harvest_score") == raw_gex_cond.get("harvest_score")
    raw_vanna_cond = raw.get("vanna_conditioned") or {}
    bar_vanna_cond = bar.VannaConditioned or {}
    assert bar_vanna_cond.get("outlook") == raw_vanna_cond.get("outlook")
    raw_macro = raw.get("macro") or {}
    bar_macro = bar.Macro or {}
    assert bar_macro.get("vix") == raw_macro.get("vix")
    assert len(bar.TermVrp or []) == len(raw.get("term_vrp") or [])
    assert len(bar.Warnings or []) == len(raw.get("warnings") or [])


def test_add_flashalpha_vrp_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_vrp
    from flashalpha_quantconnect.data.vrp import VrpBar as DirectVrpBar
    assert add_flashalpha_vrp.__module__ == "flashalpha_quantconnect.extensions"
    assert DirectVrpBar is not None
