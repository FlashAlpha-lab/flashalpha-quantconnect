# Combine FlashAlpha with equity data

Subscribe to *both* an equity bar and a FlashAlpha custom-data bar for the same ticker, and pair them by ticker string in `OnData`. This is the cleanest fix for the "two symbols" gotcha — the FlashAlpha custom-data `Symbol` is a different LEAN identity from the equity `Symbol`, and they cannot be cross-indexed.

## Problem

You want to read the FlashAlpha GEX bar and the equity bar for the same ticker in the same `OnData` callback — for example, to compare spot to the gamma flip strike before sizing a position — across an arbitrary list of tickers.

The naive `slice[_spy]` won't return the GEX bar, and `slice[_gex]` won't return the equity bar. Both subscriptions exist; you just have to ask for them separately and join by ticker.

## Solution

### C#

```csharp
using System.Collections.Generic;
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;

public class GexFlipAlgorithm : QCAlgorithm
{
    private readonly string[] _tickers = { "SPY", "QQQ", "IWM" };
    private readonly Dictionary<string, Symbol> _equity = new();
    private readonly Dictionary<string, Symbol> _gex = new();

    public override void Initialize()
    {
        SetStartDate(2024, 1, 1);
        SetEndDate(2024, 12, 31);
        SetCash(100_000);

        foreach (var ticker in _tickers)
        {
            _equity[ticker] = AddEquity(ticker, Resolution.Daily).Symbol;
            _gex[ticker]    = this.AddFlashAlphaGex(ticker).Symbol;
        }
    }

    public override void OnData(Slice slice)
    {
        foreach (var ticker in _tickers)
        {
            if (!slice.ContainsKey(_gex[ticker]) || !slice.ContainsKey(_equity[ticker])) continue;

            var gex = slice.Get<FlashAlphaGexBar>(_gex[ticker]);
            var bar = slice.Bars[_equity[ticker]];
            if (gex?.GammaFlip == null) continue;

            // Long when spot trades above the gamma flip (dealers long gamma), flat otherwise.
            var aboveFlip = bar.Close > (decimal)gex.GammaFlip.Value;
            SetHoldings(_equity[ticker], aboveFlip ? 1.0m / _tickers.Length : 0m);

            Debug($"[{Time:yyyy-MM-dd}] {ticker}  close={bar.Close:F2}  flip={gex.GammaFlip:F2}  long={aboveFlip}");
        }
    }
}
```

### Python

```python
from AlgorithmImports import *
from flashalpha_quantconnect import GexBar, add_flashalpha_gex


class GexFlipAlgorithm(QCAlgorithm):
    TICKERS = ("SPY", "QQQ", "IWM")

    def Initialize(self):
        self.SetStartDate(2024, 1, 1)
        self.SetEndDate(2024, 12, 31)
        self.SetCash(100_000)

        self.equity = {}
        self.gex = {}
        for ticker in self.TICKERS:
            self.equity[ticker] = self.AddEquity(ticker, Resolution.Daily).Symbol
            self.gex[ticker]    = add_flashalpha_gex(self, ticker).Symbol

    def OnData(self, slice):
        for ticker in self.TICKERS:
            gex_symbol = self.gex[ticker]
            eq_symbol  = self.equity[ticker]
            if gex_symbol not in slice or eq_symbol not in slice.Bars:
                continue

            gex = slice[gex_symbol]
            bar = slice.Bars[eq_symbol]
            if gex.GammaFlip is None:
                continue

            above_flip = bar.Close > gex.GammaFlip
            self.SetHoldings(eq_symbol, (1.0 / len(self.TICKERS)) if above_flip else 0.0)

            self.Debug(
                f"[{self.Time:%Y-%m-%d}] {ticker}  close={bar.Close:.2f}  "
                f"flip={gex.GammaFlip:.2f}  long={above_flip}"
            )
```

## How it works

Two parallel dictionaries — `_equity[ticker] -> equity Symbol` and `_gex[ticker] -> GEX Symbol` — are populated in `Initialize`. Each `AddEquity` and `AddFlashAlphaGex` call mints a distinct `Symbol`, and we stash both keyed by the underlying ticker string. The ticker string is the lowest-common-denominator key — both subscriptions agree on it even though their `Symbol` objects do not match.

`OnData` then walks the ticker list, looks up *both* symbols, and verifies each is present on the current slice. `slice.ContainsKey(_gex[ticker])` and `slice.Bars[_equity[ticker]]` are the right access patterns — the equity bar lives under `slice.Bars`, the custom-data bar lives at the top level of `slice`. (Python's `slice[gex_symbol]` index syntax works for custom data; equity bars need `slice.Bars[…]`.)

Once both bars are in hand, we have everything needed to compare spot to a dealer-flow level — `bar.Close` (equity close) and `gex.GammaFlip` (FlashAlpha's gamma-flip strike) — and gate the position. We size to equal-weight across the surviving longs (`1.0 / len(tickers)`) so a one-name long is never the full portfolio.

The `_gex.GammaFlip` null-check matters: on some sessions there is no zero-crossing in net dealer gamma — the API returns `null` rather than fabricating a strike. Skipping those days is more honest than picking a fallback.

## Variations

- **Use the gamma-flip distance as a sizing signal.** Instead of binary above/below, size proportionally to `(close - flip) / close`.
- **Compute volatility-of-volatility from the equity series and gate on it.** Standard LEAN indicators on `_equity[ticker]` plus the FlashAlpha gate is fully supported — both subscriptions hand you normal LEAN bars.
- **Switch GEX for ExposureSummary.** `AddFlashAlphaExposureSummary` gives you GEX *and* DEX/VEX/CHEX in one call; one bar pairs with the equity bar instead of four.
- **Universe-scoped version.** Replace the hard-coded ticker list with `FlashAlphaTickersUniverse`. The same dictionary pattern survives — wire it through `OnSecuritiesChanged`. See [filter-universe-by-gex-regime.md](filter-universe-by-gex-regime.md).
- **Use options instead of equity.** Pair `AddOption` with `AddFlashAlphaGex` on the underlying. The option chain still resolves via the LEAN option API; the FlashAlpha bar reads as above.

## Related recipes

- [Subscribe to GEX in QuantConnect](subscribe-to-gex-in-quantconnect.md) — the single-ticker version without the pairing.
- [Filter universe by GEX regime](filter-universe-by-gex-regime.md) — replaces the hard-coded ticker list with a coverage-driven universe.
- [Troubleshooting → Why two symbols?](../troubleshooting.md#why-two-symbols) — the deeper LEAN-side explanation of why custom-data and equity symbols can't be unified.
