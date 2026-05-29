"""Schema drift guard (Python).

For every FlashAlpha bar that mirrors a typed SDK ``TypedDict``,
reflect over the bar class's declared JSON-side keys and assert each
one is present on the matching SDK type. Catches silent drift when the
SDK adds (or renames) a field but the bar layer forgets to expose it.

The check is one-way (bar ⊆ DTO). The reverse — SDK fields the bar
doesn't expose — is informational only; see
``test_bar_overlaps_with_sdk_dto`` for the bare-minimum overlap check.

Bars with no typed DTO (StockQuoteBar, OptionQuoteBar) are listed in
``SKIPPED_BARS`` with a reason and excluded from the parametrise.
"""

from __future__ import annotations

import typing
from typing import Any, Dict, List, Set, Tuple, Type

import pytest

from flashalpha_historical import types as sdk_types
from flashalpha_quantconnect.data.exposure import (
    ChexBar,
    DexBar,
    ExposureLevelsBar,
    ExposureSummaryBar,
    GexBar,
    VexBar,
)
from flashalpha_quantconnect.data.max_pain import MaxPainBar
from flashalpha_quantconnect.data.narrative import NarrativeBar
from flashalpha_quantconnect.data.option_quote import OptionQuoteBar
from flashalpha_quantconnect.data.stock_quote import StockQuoteBar
from flashalpha_quantconnect.data.stock_summary import StockSummaryBar
from flashalpha_quantconnect.data.surface import SurfaceBar
from flashalpha_quantconnect.data.tickers import TickersBar
from flashalpha_quantconnect.data.volatility import AdvVolatilityBar, VolatilityBar
from flashalpha_quantconnect.data.vrp import VrpBar
from flashalpha_quantconnect.data.zero_dte import ZeroDteBar


# (Bar class, SDK TypedDict, parametrize id). 15 of 17 bars — the 2
# skipped ones (StockQuote, OptionQuote) have raw-JSON SDK responses
# with no TypedDict to diff against.
BAR_TO_DTO: List[Tuple[Type, Type, str]] = [
    (GexBar,              sdk_types.GexResponse,             "Gex"),
    (DexBar,              sdk_types.DexResponse,             "Dex"),
    (VexBar,              sdk_types.VexResponse,             "Vex"),
    (ChexBar,             sdk_types.ChexResponse,            "Chex"),
    (ExposureSummaryBar,  sdk_types.ExposureSummaryResponse, "ExposureSummary"),
    (ExposureLevelsBar,   sdk_types.ExposureLevelsResponse,  "ExposureLevels"),
    (SurfaceBar,          sdk_types.SurfaceResponse,         "Surface"),
    (MaxPainBar,          sdk_types.MaxPainResponse,         "MaxPain"),
    (VolatilityBar,       sdk_types.VolatilityResponse,      "Volatility"),
    (AdvVolatilityBar,    sdk_types.AdvVolatilityResponse,   "AdvVolatility"),
    (VrpBar,              sdk_types.VrpResponse,             "Vrp"),
    (NarrativeBar,        sdk_types.NarrativeResponse,       "Narrative"),
    (StockSummaryBar,     sdk_types.StockSummaryResponse,    "StockSummary"),
    (TickersBar,          sdk_types.TickersResponse,         "Tickers"),
]

# Bars with no typed SDK DTO — documented and excluded from drift.
SKIPPED_BARS: Dict[Type, str] = {
    StockQuoteBar:
        "SDK returns a plain dict (no TypedDict). Drift would need "
        "a hand-curated expected key set.",
    OptionQuoteBar:
        "SDK returns a list[dict] (no TypedDict). Per-row drift "
        "could be added separately if needed.",
    ZeroDteBar:
        "Python SDK does not yet declare a top-level ZeroDteResponse "
        "TypedDict for /v1/exposure/zero-dte (the C# SDK does). Once "
        "the Python SDK ships it, add the entry to BAR_TO_DTO.",
}


def _to_snake_case(pascal: str) -> str:
    out = []
    for i, c in enumerate(pascal):
        if i > 0 and c.isupper():
            out.append("_")
        out.append(c.lower())
    return "".join(out)


def _bar_json_names(bar_cls: Type) -> Set[str]:
    """JSON-side names for the bar's declared properties.

    Each annotation attribute on the class becomes a JSON name via:

    1. ``_field_aliases.get(attr, ...)`` if the bar declared an explicit
       alias (Python equivalent of C#'s ``[JsonPropertyName]``).
    2. Otherwise: ``PascalCase -> snake_case`` automatic mapping (the
       same rule ``data.source.parse`` applies at Reader time).

    Excludes attrs inherited from ``PythonDataBase`` / ``object``.
    """
    # The bar classes declare fields via PEP-526 annotations on the
    # class body — those are the JSON-side surface. Inherited attrs
    # from PythonDataBase (Symbol, Time, EndTime, Value, etc.) are
    # NOT in cls.__annotations__ — only the bar's own declarations
    # are.
    aliases = getattr(bar_cls, "_field_aliases", {}) or {}
    annotations = getattr(bar_cls, "__annotations__", {}) or {}
    return {aliases.get(attr, _to_snake_case(attr)) for attr in annotations}


def _dto_json_names(dto: Type) -> Set[str]:
    """JSON-side keys declared on the SDK TypedDict.

    Use ``typing.get_type_hints`` so forward references and the
    ``from __future__ import annotations`` lazy-eval are resolved.
    """
    try:
        hints = typing.get_type_hints(dto)
    except Exception:
        # Fallback to the raw __annotations__ — TypedDicts usually
        # resolve, but if a forward-ref chain breaks we still get
        # something useful.
        hints = dict(getattr(dto, "__annotations__", {}))
    return set(hints.keys())


@pytest.mark.parametrize(
    "bar_cls,dto",
    [(b, d) for b, d, _ in BAR_TO_DTO],
    ids=[name for _, _, name in BAR_TO_DTO],
)
def test_bar_properties_are_subset_of_sdk_dto(bar_cls: Type, dto: Type):
    """Every bar JSON name must exist on the matching SDK DTO."""
    bar_names = _bar_json_names(bar_cls)
    dto_names = _dto_json_names(dto)

    missing = bar_names - dto_names
    assert not missing, (
        f"Schema drift on {bar_cls.__name__} vs {dto.__name__}:\n"
        f"  Bar JSON names not found on SDK DTO: {sorted(missing)}\n"
        f"  SDK DTO JSON names: {sorted(dto_names)}"
    )


@pytest.mark.parametrize(
    "bar_cls,dto",
    [(b, d) for b, d, _ in BAR_TO_DTO],
    ids=[name for _, _, name in BAR_TO_DTO],
)
def test_bar_overlaps_with_sdk_dto(bar_cls: Type, dto: Type):
    """Bar must share at least one JSON name with the SDK DTO —
    guards against a mapping silently being wrong end-to-end."""
    bar_names = _bar_json_names(bar_cls)
    dto_names = _dto_json_names(dto)
    overlap = bar_names & dto_names
    assert overlap, (
        f"{bar_cls.__name__} shares ZERO field names with "
        f"{dto.__name__} — the mapping is almost certainly wrong."
    )


def test_drift_guard_covers_all_bars():
    """Every concrete ``Bar`` in the package is in ``BAR_TO_DTO`` or ``SKIPPED_BARS``.

    Catches new bars added without a drift mapping.
    """
    import flashalpha_quantconnect

    all_bars = {
        getattr(flashalpha_quantconnect, name)
        for name in flashalpha_quantconnect.__all__
        if name.endswith("Bar")
    }

    covered = {bar for bar, _, _ in BAR_TO_DTO}
    skipped = set(SKIPPED_BARS.keys())

    uncovered = all_bars - covered - skipped
    assert not uncovered, (
        f"Bars not covered by drift guard: {[b.__name__ for b in uncovered]}. "
        f"Add an entry to BAR_TO_DTO or SKIPPED_BARS in test_schema_drift_guard.py."
    )
    # Spec invariant: there are exactly 17 bars in the bridge.
    assert len(all_bars) == 17, (
        f"Expected 17 bars, found {len(all_bars)}: {sorted(b.__name__ for b in all_bars)}"
    )
