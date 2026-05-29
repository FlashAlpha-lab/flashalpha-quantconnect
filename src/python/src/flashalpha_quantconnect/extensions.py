"""Module-level sugar helpers for subscribing to FlashAlpha bars.

Python can't extend QCAlgorithm with new methods cleanly, so these are
free functions that take the algorithm as the first arg. Idiomatic call:

    from flashalpha_quantconnect import add_flashalpha_gex
    add_flashalpha_gex(self, "SPY")

Mirrors the C# ``QCAlgorithmExtensions.AddFlashAlpha*`` extension methods.
"""

from __future__ import annotations

from typing import Any

from .data.exposure import (
    ChexBar,
    DexBar,
    ExposureLevelsBar,
    ExposureSummaryBar,
    GexBar,
    VexBar,
)
from .data.max_pain import MaxPainBar
from .data.surface import SurfaceBar
from .data.zero_dte import ZeroDteBar


def _resolve_default(resolution: Any) -> Any:
    if resolution is None:
        from QuantConnect import Resolution
        resolution = Resolution.Daily
    return resolution


def add_flashalpha_gex(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to GEX bars for the given ticker.

    Equivalent to: ``algorithm.AddData(GexBar, ticker, resolution)``.

    Parameters
    ----------
    algorithm : QCAlgorithm
        The LEAN algorithm instance (typically ``self`` from inside
        ``Initialize``).
    ticker : str
        Underlying ticker (e.g. ``"SPY"``).
    resolution : Resolution, optional
        Subscription resolution. Defaults to ``Resolution.Daily``.
    """
    return algorithm.AddData(GexBar, ticker, _resolve_default(resolution))


def add_flashalpha_dex(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to DEX bars for the given ticker.

    Equivalent to: ``algorithm.AddData(DexBar, ticker, resolution)``.
    """
    return algorithm.AddData(DexBar, ticker, _resolve_default(resolution))


def add_flashalpha_vex(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to VEX bars for the given ticker.

    Equivalent to: ``algorithm.AddData(VexBar, ticker, resolution)``.
    """
    return algorithm.AddData(VexBar, ticker, _resolve_default(resolution))


def add_flashalpha_chex(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to CHEX bars for the given ticker.

    Equivalent to: ``algorithm.AddData(ChexBar, ticker, resolution)``.
    """
    return algorithm.AddData(ChexBar, ticker, _resolve_default(resolution))


def add_flashalpha_exposure_summary(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to ExposureSummary bars for the given ticker.

    Equivalent to: ``algorithm.AddData(ExposureSummaryBar, ticker, resolution)``.
    """
    return algorithm.AddData(ExposureSummaryBar, ticker, _resolve_default(resolution))


def add_flashalpha_exposure_levels(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to ExposureLevels bars for the given ticker.

    Equivalent to: ``algorithm.AddData(ExposureLevelsBar, ticker, resolution)``.
    """
    return algorithm.AddData(ExposureLevelsBar, ticker, _resolve_default(resolution))


def add_flashalpha_surface(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to volatility-surface bars for the given ticker.

    Equivalent to: ``algorithm.AddData(SurfaceBar, ticker, resolution)``.
    """
    return algorithm.AddData(SurfaceBar, ticker, _resolve_default(resolution))


def add_flashalpha_zero_dte(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to zero-DTE bars for the given ticker.

    Equivalent to: ``algorithm.AddData(ZeroDteBar, ticker, resolution)``.
    """
    return algorithm.AddData(ZeroDteBar, ticker, _resolve_default(resolution))


def add_flashalpha_max_pain(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to max-pain bars for the given ticker.

    Equivalent to: ``algorithm.AddData(MaxPainBar, ticker, resolution)``.
    """
    return algorithm.AddData(MaxPainBar, ticker, _resolve_default(resolution))
