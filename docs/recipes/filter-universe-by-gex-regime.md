# Filter universe by GEX regime

Use `FlashAlphaTickersUniverse` to pull the coverage list from the FlashAlpha API, then layer a per-name GEX subscription that drives a regime-aware holdings book. Backtest a long-only "long positive-gamma names, flat everything else" strategy across an arbitrarily large universe.

## Problem

You want to run a multi-name screener: every trading day, hold the positive-gamma names and skip the negative-gamma ones. You don't want to hard-code a ticker list — the universe should track FlashAlpha's covered symbols and gate on coverage quality (no names with less than 90 healthy days).

## Solution

### C#

```csharp
using System.Collections.Generic;
using System.Linq;
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;

public class GexRegimeUniverseAlgorithm : QCAlgorithm
{
    private readonly Dictionary<string, Symbol> _gex = new();

    public override void Initialize()
    {
        SetStartDate(2024, 1, 1);
        SetEndDate(2024, 12, 31);
        SetCash(100_000);

        UniverseSettings.Resolution = Resolution.Daily;

        // Universe: every FlashAlpha-covered name with > 90 healthy days.
        AddUniverseSelection(new FlashAlphaTickersUniverse(
            row => (row.Coverage?.HealthyDays ?? 0) > 90));
    }

    public override void OnSecuritiesChanged(SecurityChanges changes)
    {
        foreach (var added in changes.AddedSecurities)
        {
            var ticker = added.Symbol.Value;
            if (_gex.ContainsKey(ticker)) continue;
            _gex[ticker] = this.AddFlashAlphaGex(ticker).Symbol;
        }
        foreach (var removed in changes.RemovedSecurities)
        {
            var ticker = removed.Symbol.Value;
            if (_gex.TryGetValue(ticker, out var gexSymbol))
            {
                RemoveSecurity(gexSymbol);
                _gex.Remove(ticker);
            }
        }
    }

    public override void OnData(Slice slice)
    {
        var longs = new List<string>();
        foreach (var (ticker, gexSymbol) in _gex)
        {
            if (!slice.ContainsKey(gexSymbol)) continue;
            var bar = slice.Get<FlashAlphaGexBar>(gexSymbol);
            if (bar?.NetGexLabel == "positive") longs.Add(ticker);
        }
        if (longs.Count == 0) return;

        // Equal-weight the surviving names.
        var weight = 1.0m / longs.Count;
        foreach (var ticker in longs)
            SetHoldings(ticker, weight);

        // Flatten anything not in the long set.
        foreach (var holding in Portfolio.Values.Where(h => h.Invested))
            if (!longs.Contains(holding.Symbol.Value))
                Liquidate(holding.Symbol);
    }
}
```

### Python

```python
from AlgorithmImports import *
from flashalpha_quantconnect import FlashAlphaTickersUniverse, GexBar, add_flashalpha_gex


class GexRegimeUniverseAlgorithm(QCAlgorithm):
    def Initialize(self):
        self.SetStartDate(2024, 1, 1)
        self.SetEndDate(2024, 12, 31)
        self.SetCash(100_000)

        self.UniverseSettings.Resolution = Resolution.Daily

        # Universe: every FlashAlpha-covered name with > 90 healthy days.
        self.AddUniverseSelection(FlashAlphaTickersUniverse(
            filter=lambda row: row.get("coverage", {}).get("healthy_days", 0) > 90
        ))

        self.gex = {}  # ticker -> custom-data Symbol

    def OnSecuritiesChanged(self, changes):
        for added in changes.AddedSecurities:
            ticker = added.Symbol.Value
            if ticker in self.gex:
                continue
            self.gex[ticker] = add_flashalpha_gex(self, ticker).Symbol

        for removed in changes.RemovedSecurities:
            ticker = removed.Symbol.Value
            gex_symbol = self.gex.pop(ticker, None)
            if gex_symbol is not None:
                self.RemoveSecurity(gex_symbol)

    def OnData(self, slice):
        longs = []
        for ticker, gex_symbol in self.gex.items():
            if gex_symbol not in slice:
                continue
            bar = slice[gex_symbol]
            if bar.NetGexLabel == "positive":
                longs.append(ticker)

        if not longs:
            return

        weight = 1.0 / len(longs)
        for ticker in longs:
            self.SetHoldings(ticker, weight)

        # Flatten anything not in the long set.
        for holding in [h for h in self.Portfolio.Values if h.Invested]:
            if holding.Symbol.Value not in longs:
                self.Liquidate(holding.Symbol)
```

## How it works

`FlashAlphaTickersUniverse` is a LEAN `CustomUniverseSelectionModel` (C#) / `UniverseSelectionModel` (Python) that calls `historical.flashalpha.com/v1/tickers` once per day, walks the response rows, applies your predicate, and emits the surviving ticker strings to LEAN's universe-resolution pipeline. LEAN then surfaces each ticker as an equity `Symbol` in `OnSecuritiesChanged`. The predicate is fired against the raw `TickersRow` (C#) / row `dict` (Python), so you can gate on coverage span, healthy-day count, or any other field the SDK exposes.

We override `OnSecuritiesChanged` to attach a GEX subscription per name as it joins the universe (and detach it as it leaves). The custom-data Symbol returned by `AddFlashAlphaGex` is stored in a dictionary keyed by the equity ticker string — the same pattern as [combine-flashalpha-with-equity-data.md](combine-flashalpha-with-equity-data.md).

In `OnData`, we walk the dictionary, collect every ticker whose latest GEX bar is `"positive"`, and rebalance to equal weight on the survivors. Names that fall out of the long set (because they flipped to negative or no longer have a fresh bar) get liquidated.

The universe refresh interval defaults to one day, matching the upstream `tickers` endpoint's effective cadence — the coverage table doesn't change minute-to-minute. The bridge caches each per-day fetch, so multiple calls to the universe on the same date reuse the cached payload.

## Variations

- **Tighter coverage gate.** Raise `healthy_days > 90` to `> 365` for names with at least a year of clean coverage.
- **Symbol allow-list.** Combine FlashAlpha's filter with your own: `lambda row: row["symbol"] in {"SPY", "QQQ", "IWM", ...}`. The predicate is just a function.
- **Different signals on each name.** Subscribe additional bars in `OnSecuritiesChanged` — e.g. `AddFlashAlphaZeroDte(ticker)` alongside GEX — and rank in `OnData`.
- **Top-N by absolute net GEX.** Sort the surviving names by `bar.NetGex` and hold only the top 10. Same dictionary pattern, just an extra sort step before `SetHoldings`.
- **VRP-conditioned screener.** Layer `FlashAlphaVrpBar.NetHarvestScore > 70` on top of the positive-gamma gate to harvest VRP only when dealers are gamma-supportive.

## Related recipes

- [Subscribe to GEX in QuantConnect](subscribe-to-gex-in-quantconnect.md) — the single-ticker version of the GEX gate.
- [Combine FlashAlpha with equity data](combine-flashalpha-with-equity-data.md) — the equity-paired pattern you'll need if you trade off the universe with custom sizing logic.
- [0DTE pin-risk check](0dte-pin-risk-check-in-quantconnect.md) — adds a 0DTE risk filter on top.
