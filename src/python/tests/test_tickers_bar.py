"""Integration tests for TickersBar.

Mirrors test_gex_bar.py — exercise the GetSource + Reader path via
source.source_for + source.parse directly against the real API. The
``tickers`` endpoint is the special case: the SDK call is
``sdk.tickers(symbol=None)`` (no ``at``, no ticker scope) and the bar
exposes the full global coverage table. Skips without
FLASHALPHA_API_KEY (see conftest.py).
"""

from datetime import datetime
from types import SimpleNamespace

import pytest
from flashalpha_historical import FlashAlphaHistorical

from flashalpha_quantconnect import TickersBar, config
from flashalpha_quantconnect.data import source


TEST_DATE = datetime(2024, 6, 14, 15, 30, 0)
TEST_TICKER = "_universe"


@pytest.fixture(autouse=True)
def _reset_source_cache():
    source.reset()
    yield
    source.reset()


def _make_symbol(ticker: str):
    return SimpleNamespace(Value=ticker)


@pytest.mark.integration
def test_tickers_bar_fetch_and_parse_populates_tickers():
    """Layer 1: subscribe path produces a bar with a populated Tickers list."""
    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("tickers", symbol, TEST_DATE)

    assert src is not None
    line = src.Source if hasattr(src, "Source") else str(src)

    bar = source.parse(TickersBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Tickers is not None
    assert len(bar.Tickers) > 0
    assert bar.Count is not None and bar.Count > 0
    # SPY is always covered.
    symbols = {row["symbol"] for row in bar.Tickers if isinstance(row, dict)}
    assert "SPY" in symbols


@pytest.mark.integration
def test_tickers_bar_fields_match_rest_response():
    """Layer 2: bar's Tickers list matches the raw SDK response (no ``at``)."""
    sdk = FlashAlphaHistorical(api_key=config.resolve_api_key())
    # Special call shape: no ``at`` parameter, ``symbol=None`` for the list form.
    raw = sdk.tickers(symbol=None)

    symbol = _make_symbol(TEST_TICKER)
    src = source.source_for("tickers", symbol, TEST_DATE)
    line = src.Source if hasattr(src, "Source") else str(src)
    bar = source.parse(TickersBar, line, symbol, TEST_DATE)

    assert bar is not None
    assert bar.Count == raw["count"]
    assert len(bar.Tickers) == len(raw["tickers"])
    # Spot-check the first row.
    raw_first = raw["tickers"][0]
    bar_first = bar.Tickers[0]
    assert bar_first["symbol"] == raw_first["symbol"]
    assert bar_first["coverage"]["first"] == raw_first["coverage"]["first"]
    assert bar_first["coverage"]["last"] == raw_first["coverage"]["last"]
    assert bar_first["coverage"]["healthy_days"] == raw_first["coverage"]["healthy_days"]


def test_add_flashalpha_tickers_sugar_exists():
    """Compile-time test: the sugar is importable and points to the right bar class."""
    from flashalpha_quantconnect import add_flashalpha_tickers
    from flashalpha_quantconnect.data.tickers import (
        TickersBar as DirectTickersBar,
    )
    assert (
        add_flashalpha_tickers.__module__
        == "flashalpha_quantconnect.extensions"
    )
    assert DirectTickersBar is not None
