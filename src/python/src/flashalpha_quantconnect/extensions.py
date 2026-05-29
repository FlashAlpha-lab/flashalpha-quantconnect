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
from .data.narrative import NarrativeBar
from .data.option_quote import OptionQuoteBar
from .data.stock_quote import StockQuoteBar
from .data.stock_summary import StockSummaryBar
from .data.surface import SurfaceBar
from .data.tickers import TickersBar
from .data.volatility import AdvVolatilityBar, VolatilityBar
from .data.vrp import VrpBar
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


def add_flashalpha_volatility(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to volatility-analytics bars for the given ticker.

    Equivalent to: ``algorithm.AddData(VolatilityBar, ticker, resolution)``.
    """
    return algorithm.AddData(VolatilityBar, ticker, _resolve_default(resolution))


def add_flashalpha_adv_volatility(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to advanced-volatility-analytics bars for the given ticker.

    Equivalent to: ``algorithm.AddData(AdvVolatilityBar, ticker, resolution)``.
    """
    return algorithm.AddData(AdvVolatilityBar, ticker, _resolve_default(resolution))


def add_flashalpha_vrp(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    """Subscribe the algorithm to variance-risk-premium (VRP) bars for the given ticker.

    Equivalent to: ``algorithm.AddData(VrpBar, ticker, resolution)``.
    """
    return algorithm.AddData(VrpBar, ticker, _resolve_default(resolution))


def add_flashalpha_narrative(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to narrative (verbal positioning) bars for the given ticker.

    Equivalent to: ``algorithm.AddData(NarrativeBar, ticker, resolution)``.
    """
    return algorithm.AddData(NarrativeBar, ticker, _resolve_default(resolution))


def add_flashalpha_stock_summary(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to stock-summary composite bars for the given ticker.

    Equivalent to: ``algorithm.AddData(StockSummaryBar, ticker, resolution)``.
    """
    return algorithm.AddData(StockSummaryBar, ticker, _resolve_default(resolution))


def add_flashalpha_stock_quote(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to stock-quote (bid/ask/mid/last) bars for the given ticker.

    Equivalent to: ``algorithm.AddData(StockQuoteBar, ticker, resolution)``.
    """
    return algorithm.AddData(StockQuoteBar, ticker, _resolve_default(resolution))


def add_flashalpha_option_quote(
    algorithm: Any, ticker: str, resolution: Any = None
) -> Any:
    """Subscribe the algorithm to option-quote (per-contract bid/ask/mid + greeks) bars.

    Equivalent to: ``algorithm.AddData(OptionQuoteBar, ticker, resolution)``.
    """
    return algorithm.AddData(OptionQuoteBar, ticker, _resolve_default(resolution))


def add_flashalpha_tickers(
    algorithm: Any, ticker: str = "_universe", resolution: Any = None
) -> Any:
    """Subscribe the algorithm to tickers (coverage / supported-symbols) bars.

    Special case: the upstream endpoint is NOT ticker-scoped. The HTTP
    client passes ``symbol=None`` to the SDK regardless of the LEAN
    symbol on the subscription; pass any sentinel (default
    ``"_universe"``) and the bar will carry the full global coverage
    table.

    Equivalent to: ``algorithm.AddData(TickersBar, ticker, resolution)``.
    """
    return algorithm.AddData(TickersBar, ticker, _resolve_default(resolution))
