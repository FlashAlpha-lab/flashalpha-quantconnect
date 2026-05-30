"""FlashAlpha options-flow data as QuantConnect LEAN custom-data bars."""

__version__ = "0.1.1"

from . import config
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
from .data.tickers import FlashAlphaTickersUniverse, TickersBar
from .data.volatility import AdvVolatilityBar, VolatilityBar
from .data.vrp import VrpBar
from .data.zero_dte import ZeroDteBar
from .exceptions import (
    FlashAlphaAuthMissingException,
    FlashAlphaNetworkException,
    FlashAlphaQuantConnectException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)
from .extensions import (
    add_flashalpha_adv_volatility,
    add_flashalpha_chex,
    add_flashalpha_dex,
    add_flashalpha_exposure_levels,
    add_flashalpha_exposure_summary,
    add_flashalpha_gex,
    add_flashalpha_max_pain,
    add_flashalpha_narrative,
    add_flashalpha_option_quote,
    add_flashalpha_stock_quote,
    add_flashalpha_stock_summary,
    add_flashalpha_surface,
    add_flashalpha_tickers,
    add_flashalpha_vex,
    add_flashalpha_volatility,
    add_flashalpha_vrp,
    add_flashalpha_zero_dte,
)

__all__ = [
    "config",
    "GexBar",
    "DexBar",
    "VexBar",
    "ChexBar",
    "ExposureSummaryBar",
    "ExposureLevelsBar",
    "SurfaceBar",
    "ZeroDteBar",
    "MaxPainBar",
    "VolatilityBar",
    "AdvVolatilityBar",
    "VrpBar",
    "NarrativeBar",
    "StockSummaryBar",
    "StockQuoteBar",
    "OptionQuoteBar",
    "TickersBar",
    "FlashAlphaTickersUniverse",
    "add_flashalpha_gex",
    "add_flashalpha_dex",
    "add_flashalpha_vex",
    "add_flashalpha_chex",
    "add_flashalpha_exposure_summary",
    "add_flashalpha_exposure_levels",
    "add_flashalpha_surface",
    "add_flashalpha_zero_dte",
    "add_flashalpha_max_pain",
    "add_flashalpha_volatility",
    "add_flashalpha_adv_volatility",
    "add_flashalpha_vrp",
    "add_flashalpha_narrative",
    "add_flashalpha_stock_summary",
    "add_flashalpha_stock_quote",
    "add_flashalpha_option_quote",
    "add_flashalpha_tickers",
    "FlashAlphaAuthMissingException",
    "FlashAlphaNetworkException",
    "FlashAlphaQuantConnectException",
    "FlashAlphaRateLimitedException",
    "FlashAlphaUnauthorizedException",
]
