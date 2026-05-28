"""Integration tests for FlashAlphaHttpClient.

Per the project's test philosophy (integration only — no mocks):
this file hits the real `historical.flashalpha.com` API via the SDK
the bridge wraps. Requires `FLASHALPHA_API_KEY` in the environment;
the conftest skip hook handles its absence.
"""

from datetime import datetime

import pytest

from flashalpha_quantconnect.client import FlashAlphaHttpClient


@pytest.mark.integration
def test_fetch_gex_json_returns_payload():
    """Real-API round trip: SDK → bridge → dict with expected shape."""
    client = FlashAlphaHttpClient()
    raw = client.fetch_json(
        endpoint="exposure/gex",
        ticker="SPY",
        at=datetime(2024, 6, 14, 15, 30, 0),  # RTH timestamp, matches C# sibling
    )

    assert isinstance(raw, dict)
    assert raw.get("symbol") == "SPY"
    assert "net_gex" in raw
