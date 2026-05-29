"""Minimal real LEAN algorithm — Python twin of the C# fixture.

The strategy is intentionally simple: long SPY when net GEX is positive
(long-gamma dealer regime), flat when it flips negative. The point is to
have a real LEAN-runnable algorithm whose backtest result can be diffed
against a committed golden file when the SDK or bar layer changes a
number.

Outside the LEAN runtime — pytest, IDE — the QC* imports happen lazily
inside ``Initialize``, so this module is importable without LEAN.
"""

from __future__ import annotations

from typing import Any


class GexRegimeFollowingAlgorithm:
    """Stand-alone class body for the LEAN algorithm.

    LEAN injects ``QCAlgorithm`` as the base at deserialisation; from
    pytest's perspective this is a plain class and the LEAN-only
    overrides are just methods that won't run unless LEAN drives them.
    """

    def Initialize(self) -> None:  # pragma: no cover - exercised by LEAN
        from QuantConnect import Resolution

        from flashalpha_quantconnect import add_flashalpha_gex

        self.SetStartDate(2024, 6, 3)
        self.SetEndDate(2024, 6, 14)
        self.SetCash(100_000)

        self._equity = self.AddEquity("SPY", Resolution.Daily).Symbol
        self._gex = add_flashalpha_gex(self, "SPY").Symbol

    def OnData(self, slice: Any) -> None:  # pragma: no cover - exercised by LEAN
        if not slice.ContainsKey(self._gex):
            return
        bar = slice[self._gex]
        if bar is None or getattr(bar, "NetGex", None) is None:
            return

        # Long-gamma regime: dealers absorb vol — comfortable being long.
        # Short-gamma regime: dealers amplify moves — flatten.
        target_weight = 1.0 if bar.NetGex > 0 else 0.0
        self.SetHoldings(self._equity, target_weight)

    def OnOrderEvent(self, order_event: Any) -> None:  # pragma: no cover - LEAN
        # Logged so diffs against the trade-count golden are easy to debug.
        try:
            from QuantConnect.Orders import OrderStatus

            if order_event.Status == OrderStatus.Filled:
                self.Log(
                    f"FILL: {order_event.Symbol.Value} "
                    f"{order_event.FillQuantity} @ {order_event.FillPrice}"
                )
        except Exception:
            pass
