"""Module-level sugar helpers for subscribing to FlashAlpha bars.

Python can't extend QCAlgorithm with new methods cleanly, so these are
free functions that take the algorithm as the first arg. Idiomatic call:

    from flashalpha_quantconnect import add_flashalpha_gex
    add_flashalpha_gex(self, "SPY")

Mirrors the C# ``QCAlgorithmExtensions.AddFlashAlphaGex`` extension method.
"""

from __future__ import annotations

from typing import Any

from .data.exposure import GexBar


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
    if resolution is None:
        from QuantConnect import Resolution
        resolution = Resolution.Daily
    return algorithm.AddData(GexBar, ticker, resolution)
