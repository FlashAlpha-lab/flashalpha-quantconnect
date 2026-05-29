# 0DTE pin-risk check in QuantConnect

Use `FlashAlphaZeroDteBar` to gate same-day-expiry trades on the dealer pin-risk score. Skip entries when the pin score is high (spot is going to magnet to a strike all session) or when the underlying has no 0DTE expiry at all.

## Problem

You run a 0DTE strategy and want to avoid days where the pin-risk components — strike magnet effect, dealer hedging concentration, low expected move — make your directional thesis a coin flip. You also need the algorithm to gracefully handle tickers without same-day expiries (`NoZeroDte == true`).

## Solution

### C#

```csharp
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;

public class ZeroDtePinCheckAlgorithm : QCAlgorithm
{
    private const int PinScoreCutoff = 60;
    private Symbol _spy;
    private Symbol _zdte;

    public override void Initialize()
    {
        SetStartDate(2024, 1, 1);
        SetEndDate(2024, 12, 31);
        SetCash(100_000);

        _spy  = AddEquity("SPY", Resolution.Daily).Symbol;
        _zdte = this.AddFlashAlphaZeroDte("SPY").Symbol;
    }

    public override void OnData(Slice slice)
    {
        if (!slice.ContainsKey(_zdte)) return;
        var bar = slice.Get<FlashAlphaZeroDteBar>(_zdte);
        if (bar == null) return;

        // Thin response — no same-day expiry today. Skip.
        if (bar.NoZeroDte == true)
        {
            Debug($"[{Time:yyyy-MM-dd}] no 0DTE — next {bar.NextZeroDteExpiry}");
            Liquidate(_spy);
            return;
        }

        var pinScore = bar.PinRisk?.Score;
        var magnet   = bar.PinRisk?.MagnetStrike;
        var sigmaRemain = bar.ExpectedMove?.SigmaRemaining;

        if (pinScore == null) { Liquidate(_spy); return; }

        // High pin risk → skip the directional trade (spot likely pins).
        if (pinScore.Value >= PinScoreCutoff)
        {
            Debug($"[{Time:yyyy-MM-dd}] PIN risk={pinScore}  magnet={magnet:F2}  skipping");
            Liquidate(_spy);
            return;
        }

        // Otherwise long the underlying with size scaled to the remaining-session sigma.
        var targetWeight = sigmaRemain.HasValue && sigmaRemain.Value > 1.0 ? 1.0m : 0.5m;
        SetHoldings(_spy, targetWeight);

        Debug($"[{Time:yyyy-MM-dd}] long w={targetWeight}  pin={pinScore}  σ_remain={sigmaRemain:F2}");
    }
}
```

### Python

```python
from AlgorithmImports import *
from flashalpha_quantconnect import ZeroDteBar, add_flashalpha_zero_dte


class ZeroDtePinCheckAlgorithm(QCAlgorithm):
    PIN_SCORE_CUTOFF = 60

    def Initialize(self):
        self.SetStartDate(2024, 1, 1)
        self.SetEndDate(2024, 12, 31)
        self.SetCash(100_000)

        self.spy  = self.AddEquity("SPY", Resolution.Daily).Symbol
        self.zdte = add_flashalpha_zero_dte(self, "SPY").Symbol

    def OnData(self, slice):
        if self.zdte not in slice:
            return
        bar = slice[self.zdte]

        # Thin response — no same-day expiry today. Skip.
        if bar.NoZeroDte:
            self.Debug(f"[{self.Time:%Y-%m-%d}] no 0DTE — next {bar.NextZeroDteExpiry}")
            self.Liquidate(self.spy)
            return

        pin_risk = bar.PinRisk or {}
        pin_score = pin_risk.get("score")
        magnet = pin_risk.get("magnet_strike")
        sigma_remain = (bar.ExpectedMove or {}).get("sigma_remaining")

        if pin_score is None:
            self.Liquidate(self.spy)
            return

        if pin_score >= self.PIN_SCORE_CUTOFF:
            self.Debug(
                f"[{self.Time:%Y-%m-%d}] PIN risk={pin_score}  "
                f"magnet={magnet}  skipping"
            )
            self.Liquidate(self.spy)
            return

        target_weight = 1.0 if (sigma_remain or 0) > 1.0 else 0.5
        self.SetHoldings(self.spy, target_weight)

        self.Debug(
            f"[{self.Time:%Y-%m-%d}] long w={target_weight}  "
            f"pin={pin_score}  σ_remain={sigma_remain}"
        )
```

## How it works

`FlashAlphaZeroDteBar` is the richest bar in the bridge. Each session it carries a `PinRisk` block with a 0-100 `Score`, a `MagnetStrike`, and a component breakdown explaining why the score is what it is (`gex` share, `oi` share, distance-to-magnet share). Alongside it sits `ExpectedMove` with `SigmaFull` (the full-session implied move at 1 sigma) and `SigmaRemaining` (what's left from now to close).

The pin-risk gate works like this: if `PinRisk.Score` is at or above our cutoff (60 in the example), the dealer-flow + OI concentration is high enough that spot is statistically likely to pin to `MagnetStrike` all session — bad for directional 0DTE bets. We liquidate and wait.

When the score is below the cutoff, we size proportionally to how much room the underlying has left: at the open `SigmaRemaining` is the full-session implied 1-sigma move, by midday it's roughly half, by close it's near zero. We use it as a coarse "is there time for this trade to work" gate — full weight when more than $1 of remaining 1-sigma motion is implied, half weight otherwise.

The `NoZeroDte` check is critical. Not every ticker has a 0DTE expiry every session — single names rarely do, and even ETFs skip some sessions. When the API returns a "thin" response, **every nested block is null** except `NoZeroDte`, `Message`, and `NextZeroDteExpiry`. Skipping the trade is the only correct behavior. The bar still arrives in `OnData` so your `OnData` is the right place to enforce this; there is no separate "skip" event.

## Variations

- **Different cutoff per regime.** Read `bar.Regime?.Label` and use a tighter cutoff (e.g. 50) on `"positive_gamma"` days where pinning is more reliable.
- **Trade against the pin instead of skipping.** When `PinRisk.Score` is high and spot is below `MagnetStrike`, go long (and the inverse when spot is above). Replaces the `Liquidate` branch with another `SetHoldings`.
- **Layer the hedging-flow estimate.** Read `bar.Hedging` for projected dealer rebalance at ±1% / ±2% spot moves; use it to model a directional bias on top of the pin filter.
- **Decay-aware sizing.** Read `bar.Decay` for the theta + charm regime and shrink size when charm is large (delta drifting fast as time decays).
- **Multi-ticker.** Same dictionary pattern as [combine-flashalpha-with-equity-data.md](combine-flashalpha-with-equity-data.md). Per-ticker pin gates feed an aggregate book.

## Related recipes

- [Subscribe to GEX in QuantConnect](subscribe-to-gex-in-quantconnect.md) — start here if you've never wired a FlashAlpha bar.
- [Combine FlashAlpha with equity data](combine-flashalpha-with-equity-data.md) — the multi-ticker pattern when you need the equity bar in the same callback.
- [Vol-surface snapshot in QuantConnect](vol-surface-snapshot-in-quantconnect.md) — a complementary IV view for 0DTE strategies.
