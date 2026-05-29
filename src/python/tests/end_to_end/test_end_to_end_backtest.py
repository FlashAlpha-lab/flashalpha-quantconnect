"""Layer-3 end-to-end backtest assertion shape (Python).

Same flow as the C# counterpart: an external harness runs the LEAN
backtest of ``GexRegimeFollowingAlgorithm``, captures the final stats,
and writes them to ``tests/golden/end_to_end.json``; this test reads
the golden and compares.

Currently skipped — the local sandbox cannot run a real LEAN backtest
end-to-end. Run with ``pytest -m end_to_end`` once a LEAN harness is
wired. The placeholder golden file carries ``_status: "PLACEHOLDER"`` so
that a stale placeholder cannot silently masquerade as a passing real
backtest.
"""

from __future__ import annotations

import json
import os
from pathlib import Path

import pytest


GOLDEN_PATH = Path(__file__).resolve().parent.parent / "golden" / "end_to_end.json"


def test_golden_parses_cleanly():
    """Always runs (no LEAN required). Guards against accidental
    corruption of the committed golden file."""
    assert GOLDEN_PATH.exists(), f"golden file missing: {GOLDEN_PATH}"
    data = json.loads(GOLDEN_PATH.read_text(encoding="utf-8"))
    assert "final_equity" in data
    assert "total_trades" in data
    assert "sharpe" in data


@pytest.mark.end_to_end
def test_gex_regime_following_matches_golden():
    """Re-runs the LEAN backtest and asserts on the committed golden.
    Skipped without a LEAN harness — see the module docstring."""
    harness = os.environ.get("FLASHALPHA_LEAN_HARNESS")
    if not harness:
        pytest.skip("FLASHALPHA_LEAN_HARNESS not set — LEAN harness required")

    golden = json.loads(GOLDEN_PATH.read_text(encoding="utf-8"))
    status = golden.get("_status", "")
    if isinstance(status, str) and status.startswith("PLACEHOLDER"):
        pytest.fail(
            "Golden file is still the placeholder. Re-run the LEAN harness "
            "to capture real numbers before enabling this test."
        )

    # TODO: when the LEAN harness is wired, drive the engine here, run
    # the backtest, and assert on the captured stats:
    #
    #     from .gex_regime_following_algorithm import GexRegimeFollowingAlgorithm
    #     result = lean_harness.run(GexRegimeFollowingAlgorithm)
    #     assert abs(result.final_equity - golden["final_equity"]) < 0.01
    #     assert result.total_trades == golden["total_trades"]
    #     assert abs(result.sharpe - golden["sharpe"]) < 1e-3

    pytest.fail(
        "Harness env var is set but the LEAN-engine wiring isn't "
        "implemented yet — wire lean_harness.run() into this test."
    )


def test_algorithm_module_importable():
    """Compile-time check: the algorithm module imports without LEAN."""
    from tests.end_to_end.gex_regime_following_algorithm import (
        GexRegimeFollowingAlgorithm,
    )

    assert GexRegimeFollowingAlgorithm is not None
    # The LEAN-only methods exist on the class — they just won't run
    # outside LEAN because their bodies import QC types lazily.
    assert hasattr(GexRegimeFollowingAlgorithm, "Initialize")
    assert hasattr(GexRegimeFollowingAlgorithm, "OnData")
