"""Tests for the FlashAlphaTickersUniverse helper.

Wiring + filter tests run without an API key by mocking the HTTP
client. The live integration test (``test_universe_live_fetch...``)
skips when ``FLASHALPHA_API_KEY`` isn't set, matching the rest of the
suite.

The LEAN-side ``CreateUniverses`` is exercised by LEAN Cloud at runtime
and isn't reachable from pytest — the fallback path that returns ``[]``
when the LEAN types aren't importable is what we verify here.
"""

from __future__ import annotations

from datetime import datetime
from typing import Any

import pytest

from flashalpha_quantconnect import FlashAlphaTickersUniverse
from flashalpha_quantconnect.data import source


class _MockClient:
    """Fake of FlashAlphaHttpClient — returns a fixed JSON payload as a dict
    (matching the real client's parsed-JSON contract)."""

    def __init__(self, payload: Any):
        self._payload = payload

    def fetch_json(self, endpoint: str, ticker: str, at: Any) -> Any:
        return self._payload


@pytest.fixture(autouse=True)
def _reset_source_cache():
    source.reset()
    yield
    source.reset()


def test_universe_default_ctor_is_constructible():
    universe = FlashAlphaTickersUniverse()
    assert universe is not None


def test_universe_with_filter_is_constructible():
    universe = FlashAlphaTickersUniverse(
        filter=lambda row: row.get("coverage", {}).get("healthy_days", 0) > 30
    )
    assert universe is not None


def test_universe_applies_filter():
    payload = {
        "tickers": [
            {"symbol": "SPY", "coverage": {"first": "2020-01-01", "last": "2024-12-31", "healthy_days": 1200}},
            {"symbol": "QQQ", "coverage": {"first": "2020-01-01", "last": "2024-12-31", "healthy_days": 1180}},
            {"symbol": "SPARSE", "coverage": {"first": "2024-06-01", "last": "2024-06-30", "healthy_days": 20}},
        ],
        "count": 3,
    }
    universe = FlashAlphaTickersUniverse(
        filter=lambda row: row.get("coverage", {}).get("healthy_days", 0) > 100,
        client=_MockClient(payload),
    )
    tickers = universe.select_tickers(datetime(2024, 6, 14))
    assert set(tickers) == {"SPY", "QQQ"}


def test_universe_no_filter_yields_every_row():
    payload = {
        "tickers": [
            {"symbol": "SPY", "coverage": {"healthy_days": 1200}},
            {"symbol": "QQQ", "coverage": {"healthy_days": 1180}},
            {"symbol": "IWM", "coverage": {"healthy_days": 900}},
        ],
        "count": 3,
    }
    universe = FlashAlphaTickersUniverse(client=_MockClient(payload))
    tickers = universe.select_tickers(datetime(2024, 6, 14))
    assert set(tickers) == {"SPY", "QQQ", "IWM"}


def test_universe_skips_rows_with_missing_symbol():
    payload = {
        "tickers": [
            {"symbol": "SPY", "coverage": {"healthy_days": 1200}},
            {"coverage": {"healthy_days": 500}},
            {"symbol": "", "coverage": {"healthy_days": 500}},
        ],
        "count": 3,
    }
    universe = FlashAlphaTickersUniverse(client=_MockClient(payload))
    tickers = universe.select_tickers(datetime(2024, 6, 14))
    assert tickers == ["SPY"]


def test_universe_empty_response_yields_empty_universe():
    universe = FlashAlphaTickersUniverse(client=_MockClient({}))
    assert universe.select_tickers(datetime(2024, 6, 14)) == []


def test_universe_malformed_response_yields_empty_universe():
    # A real client may return a non-dict (e.g. on transport error a
    # caller surfaces ``None`` or a string). Guard against that.
    universe = FlashAlphaTickersUniverse(client=_MockClient("error-string"))
    assert universe.select_tickers(datetime(2024, 6, 14)) == []


def test_universe_create_universes_fallback_returns_empty_without_lean():
    """Outside the LEAN runtime, CreateUniverses returns an empty list —
    the LEAN type imports inside the method fail and the fallback path
    kicks in. This proves the helper is importable in pytest without
    blowing up at AddUniverseSelection time during dry-runs."""

    class _FakeAlgo:
        def Symbol(self, *args: Any, **kwargs: Any) -> Any:  # noqa: N802
            raise AssertionError("Symbol() must not be called outside LEAN")

    universe = FlashAlphaTickersUniverse()
    # In a non-LEAN environment the QuantConnect.Data.UniverseSelection
    # imports inside CreateUniverses fail and we return [].
    assert universe.CreateUniverses(_FakeAlgo()) == []


@pytest.mark.integration
def test_universe_live_fetch_narrows_universe():
    unfiltered = FlashAlphaTickersUniverse()
    all_tickers = unfiltered.select_tickers(datetime.utcnow())

    heavily_covered = FlashAlphaTickersUniverse(
        filter=lambda row: row.get("coverage", {}).get("healthy_days", 0) > 100
    )
    filtered = heavily_covered.select_tickers(datetime.utcnow())

    assert len(all_tickers) > 0
    # SPY survives a sane coverage filter.
    assert "SPY" in filtered
    # Filtered set is a subset of the unfiltered one.
    assert set(filtered).issubset(set(all_tickers))


def test_add_flashalpha_tickers_universe_exported():
    """Re-export check — FlashAlphaTickersUniverse is importable from the package root."""
    from flashalpha_quantconnect import FlashAlphaTickersUniverse as Exported

    assert Exported is FlashAlphaTickersUniverse
