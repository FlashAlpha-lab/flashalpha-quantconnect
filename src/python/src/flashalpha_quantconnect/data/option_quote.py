"""Option-quote bar — one row per contract in the chain.

Mirrors the JSON response from ``GET /v1/optionquote/{symbol}?at=...``.
With all three filters (``expiry`` + ``strike`` + ``type``) the upstream
endpoint returns a single object; bar subscriptions only carry
``(ticker, date)`` so the bar always requests the unfiltered form and
the API returns a JSON ARRAY (not an object). The bar exposes that
array verbatim as ``Quotes``.

Historical-specific gaps (from the SDK docstring):

- Per-row ``bidSize`` / ``askSize`` are always ``0`` — minute table has
  no sizes.
- Per-row ``volume`` is always ``0``.
- Per-row ``svi_vol`` is always ``None`` with
  ``svi_vol_gated == "backtest_mode"``.

Reader override: the upstream root is an array, not an object, so this
bar bypasses :func:`flashalpha_quantconnect.data.source.parse` (which
assumes object roots) and deserialises the array directly into
``Quotes``.
"""

from __future__ import annotations

import json
from datetime import timedelta
from typing import Any, List, Optional

from ._lean_import import PythonDataBase
from . import source as _source


class OptionQuoteBar(PythonDataBase):
    """Option-quote bar — full option chain at the requested minute.

    Mirrors the JSON array response from ``GET /v1/optionquote/{symbol}?at=...``.

    Subscribe with: ``algo.AddData(OptionQuoteBar, ticker, Resolution.Daily)``
    or the sugar ``add_flashalpha_option_quote(algo, "SPY")``.

    ``Quotes`` is a list of dicts, one per contract — each dict contains
    ``type`` (``"C"`` / ``"P"``), ``expiry``, ``strike``, ``bid``,
    ``ask``, ``mid``, plus first- and second-order greeks
    (``delta``, ``gamma``, ``theta``, ``vega``, ``rho``, ``vanna``,
    ``charm``), ``implied_vol`` / ``iv_bid`` / ``iv_ask``,
    ``open_interest``, and the documented zero-on-historical fields.
    """

    Quotes: Optional[List[Any]] = None

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return _source.source_for("option/quote", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        bar = OptionQuoteBar()
        bar.Symbol = config.Symbol
        bar.Time = date
        bar.EndTime = date + timedelta(days=1)

        payload = _source._resolve_json(line)
        if not payload:
            return bar
        try:
            obj = json.loads(payload)
        except (TypeError, ValueError):
            return bar

        if isinstance(obj, list):
            bar.Quotes = obj
        elif isinstance(obj, dict):
            # Filtered single-object response — wrap into a 1-element list
            # so downstream code can treat Quotes uniformly.
            bar.Quotes = [obj]
        return bar
