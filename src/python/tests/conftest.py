import os
import pytest


def pytest_collection_modifyitems(config, items):
    """Skip integration tests when FLASHALPHA_API_KEY is not set."""
    if os.environ.get("FLASHALPHA_API_KEY"):
        return
    skip_integration = pytest.mark.skip(reason="FLASHALPHA_API_KEY not set")
    for item in items:
        if "integration" in item.keywords:
            item.add_marker(skip_integration)
