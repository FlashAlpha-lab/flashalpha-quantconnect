# Subscribe to GEX in QuantConnect

The baseline recipe. Subscribe to `FlashAlphaGexBar` for a single ticker, read the headline `NetGex` and the `NetGexLabel` regime label in `OnData`, and trade off it. Five-minute starter template, both languages.

## Problem

You want a backtest that gates SPY exposure on FlashAlpha's net dealer gamma — long when dealers are net long gamma (`NetGexLabel == "positive"`), flat otherwise — without writing any data ingestion code.

## Solution

### C#

```csharp
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;

public class GexRegimeAlgorithm : QCAlgorithm
{
    private Symbol _spy;
    private Symbol _gex;

    public override void Initialize()
    {
        SetStartDate(2024, 1, 1);
        SetEndDate(2024, 12, 31);
        SetCash(100_000);

        // Equity drives PnL.
        _spy = AddEquity("SPY", Resolution.Daily).Symbol;

        // GEX drives the regime gate.
        // Equivalent: AddData<FlashAlphaGexBar>("SPY", Resolution.Daily).Symbol.
        _gex = this.AddFlashAlphaGex("SPY").Symbol;
    }

    public override void OnData(Slice slice)
    {
        if (!slice.ContainsKey(_gex)) return;
        var bar = slice.Get<FlashAlphaGexBar>(_gex);
        if (bar == null) return;

        var longRegime = bar.NetGexLabel == "positive";
        SetHoldings(_spy, longRegime ? 1.0m : 0m);

        Debug($"[{Time:yyyy-MM-dd}] net_gex={bar.NetGex:F0}  label={bar.NetGexLabel}  flip={bar.GammaFlip:F2}");
    }
}
```

### Python

```python
from AlgorithmImports import *
from flashalpha_quantconnect import GexBar, add_flashalpha_gex


class GexRegimeAlgorithm(QCAlgorithm):
    def Initialize(self):
        self.SetStartDate(2024, 1, 1)
        self.SetEndDate(2024, 12, 31)
        self.SetCash(100_000)

        # Equity drives PnL.
        self.spy = self.AddEquity("SPY", Resolution.Daily).Symbol

        # GEX drives the regime gate.
        # Equivalent: self.AddData(GexBar, "SPY", Resolution.Daily).Symbol.
        self.gex = add_flashalpha_gex(self, "SPY").Symbol

    def OnData(self, slice):
        if self.gex not in slice:
            return
        bar = slice[self.gex]

        long_regime = bar.NetGexLabel == "positive"
        self.SetHoldings(self.spy, 1.0 if long_regime else 0.0)

        self.Debug(
            f"[{self.Time:%Y-%m-%d}] net_gex={bar.NetGex:.0f}  "
            f"label={bar.NetGexLabel}  flip={bar.GammaFlip:.2f}"
        )
```

## How it works

The two `Add*` calls register two distinct subscriptions with LEAN:

- `AddEquity("SPY")` returns the equity `Symbol`. This is the symbol you trade against — `SetHoldings(_spy, …)` opens or sizes a position in SPY shares.
- `AddFlashAlphaGex("SPY")` (or `add_flashalpha_gex(self, "SPY")` in Python) creates a *custom-data* subscription whose `Symbol` is a separate LEAN identity. It's the symbol you look up in `slice` to read the FlashAlpha bar.

The bar arrives in `OnData` at the same daily cadence as the equity bar. On a daily backtest the bridge fires one HTTP request to `historical.flashalpha.com` per ticker per trading day; the bar's `NetGexLabel` resolves to `"positive"` or `"negative"` (and very occasionally `"unknown"` on data-quality-flagged sessions). `SetHoldings(_spy, …)` either targets full-portfolio SPY exposure or zero — flat — based on the label.

We keep the equity at `Resolution.Daily` to match the GEX cadence. Higher resolution on the equity side is fine but pays nothing here — the strategy only acts when the GEX bar fires.

The `Debug` line is purely for observability; remove it for production runs to keep the log clean.

## Variations

- **Long/short instead of long/flat.** `SetHoldings(_spy, longRegime ? 1.0m : -0.5m)` to short into negative-gamma days.
- **Trade IV instead of spot.** Swap `SetHoldings(_spy, …)` for an options leg using LEAN's standard options API. The GEX label still drives the gate; the position changes.
- **Multiple tickers.** Index into a dictionary: subscribe `_gex["SPY"]`, `_gex["QQQ"]`, `_gex["IWM"]` and gate each independently. The pattern is in [combine-flashalpha-with-equity-data.md](combine-flashalpha-with-equity-data.md).
- **Layer a vol filter.** Subscribe `FlashAlphaVolatilityBar` on the same ticker; require `bar.AtmIv < 0.20` in addition to the positive-gamma gate.
- **Hourly cadence.** Pass `Resolution.Hour` to both `AddEquity` and `AddFlashAlphaGex`. The bridge fires ~7 calls per ticker per session at hourly resolution.

## Related recipes

- [Combine FlashAlpha with equity data](combine-flashalpha-with-equity-data.md) — the pattern when you need both bars in the same `OnData` callback and multiple tickers.
- [Filter universe by GEX regime](filter-universe-by-gex-regime.md) — turn the single-ticker gate into a multi-name screener via `FlashAlphaTickersUniverse`.
- [0DTE pin-risk check](0dte-pin-risk-check-in-quantconnect.md) — gate same-day-expiry trades on `FlashAlphaZeroDteBar.PinRisk`.
