import os
import pytest


def pytest_collection_modifyitems(config, items):
    """Skip integration tests when FLASHALPHA_API_KEY is not set, and
    skip end-to-end tests when FLASHALPHA_LEAN_HARNESS is not set."""
    has_api_key = bool(os.environ.get("FLASHALPHA_API_KEY"))
    has_lean_harness = bool(os.environ.get("FLASHALPHA_LEAN_HARNESS"))

    skip_integration = pytest.mark.skip(reason="FLASHALPHA_API_KEY not set")
    skip_end_to_end = pytest.mark.skip(reason="FLASHALPHA_LEAN_HARNESS not set")

    for item in items:
        # Use get_closest_marker to look at explicit @pytest.mark.X
        # decorators only — `item.keywords` also includes module / path
        # names, which would incorrectly match tests sitting in a
        # directory like ``tests/end_to_end/``.
        if item.get_closest_marker("integration") and not has_api_key:
            item.add_marker(skip_integration)
        if item.get_closest_marker("end_to_end") and not has_lean_harness:
            item.add_marker(skip_end_to_end)
