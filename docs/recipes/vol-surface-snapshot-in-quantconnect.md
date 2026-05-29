# Vol-surface snapshot in QuantConnect

Use `FlashAlphaSurfaceBar` to read the smoothed implied-vol surface at a specific timestamp, walk the tenor × moneyness grid, and bilinearly interpolate IV at any arbitrary `(tenor, moneyness)` point. Useful as a research scratchpad or to feed a downstream pricing model that needs IV between grid points.

## Problem

You want IV at a non-grid point — say, the 22-day tenor at 1.03 moneyness — without rolling your own surface fit. The FlashAlpha surface ships a 5×5 (or 7×7, depending on plan) grid; you need a clean way to interpolate.

## Solution

### C#

```csharp
using System;
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;

public class VolSurfaceSnapshotAlgorithm : QCAlgorithm
{
    private Symbol _surface;

    public override void Initialize()
    {
        SetStartDate(2024, 6, 14);
        SetEndDate(2024, 6, 14);
        SetCash(100_000);

        _surface = this.AddFlashAlphaSurface("SPY").Symbol;
    }

    public override void OnData(Slice slice)
    {
        if (!slice.ContainsKey(_surface)) return;
        var bar = slice.Get<FlashAlphaSurfaceBar>(_surface);
        if (bar?.Iv == null || bar.Tenors == null || bar.Moneyness == null) return;

        // ATM front: tenor[0], middle moneyness column.
        var atmFront = bar.Iv[0][bar.Moneyness.Length / 2];
        Debug($"front ATM IV={atmFront:F3}");

        // Interpolate IV at (tenor=22 days, moneyness=1.03).
        var iv = BilinearIv(bar, tenor: 22.0 / 365.0, moneyness: 1.03);
        Debug($"IV(22d, 1.03) = {iv:F4}");
    }

    private static double BilinearIv(FlashAlphaSurfaceBar bar, double tenor, double moneyness)
    {
        // Clamp into the grid; FlashAlpha surfaces aren't extrapolation-safe.
        var t = Math.Clamp(tenor, bar.Tenors![0], bar.Tenors[^1]);
        var m = Math.Clamp(moneyness, bar.Moneyness![0], bar.Moneyness[^1]);

        var ti = LowerIndex(bar.Tenors, t);
        var mi = LowerIndex(bar.Moneyness, m);

        var t0 = bar.Tenors[ti];     var t1 = bar.Tenors[ti + 1];
        var m0 = bar.Moneyness[mi];  var m1 = bar.Moneyness[mi + 1];

        var iv00 = bar.Iv![ti][mi];
        var iv01 = bar.Iv[ti][mi + 1];
        var iv10 = bar.Iv[ti + 1][mi];
        var iv11 = bar.Iv[ti + 1][mi + 1];

        var wt = (t - t0) / (t1 - t0);
        var wm = (m - m0) / (m1 - m0);

        return (1 - wt) * (1 - wm) * iv00
             + (1 - wt) *      wm  * iv01
             +      wt  * (1 - wm) * iv10
             +      wt  *      wm  * iv11;
    }

    private static int LowerIndex(double[] grid, double v)
    {
        for (int i = 0; i < grid.Length - 1; i++)
            if (v <= grid[i + 1]) return i;
        return grid.Length - 2;
    }
}
```

### Python

```python
from AlgorithmImports import *
from flashalpha_quantconnect import SurfaceBar, add_flashalpha_surface


class VolSurfaceSnapshotAlgorithm(QCAlgorithm):
    def Initialize(self):
        self.SetStartDate(2024, 6, 14)
        self.SetEndDate(2024, 6, 14)
        self.SetCash(100_000)

        self.surface = add_flashalpha_surface(self, "SPY").Symbol

    def OnData(self, slice):
        if self.surface not in slice:
            return
        bar = slice[self.surface]
        if not bar.Iv or not bar.Tenors or not bar.Moneyness:
            return

        # ATM front: tenor[0], middle moneyness column.
        atm_front = bar.Iv[0][len(bar.Moneyness) // 2]
        self.Debug(f"front ATM IV={atm_front:.3f}")

        iv = self._bilinear_iv(bar, tenor=22.0 / 365.0, moneyness=1.03)
        self.Debug(f"IV(22d, 1.03) = {iv:.4f}")

    @staticmethod
    def _bilinear_iv(bar, tenor: float, moneyness: float) -> float:
        tenors = bar.Tenors
        moneyness_grid = bar.Moneyness

        # Clamp into the grid; FlashAlpha surfaces aren't extrapolation-safe.
        t = min(max(tenor, tenors[0]), tenors[-1])
        m = min(max(moneyness, moneyness_grid[0]), moneyness_grid[-1])

        ti = VolSurfaceSnapshotAlgorithm._lower_index(tenors, t)
        mi = VolSurfaceSnapshotAlgorithm._lower_index(moneyness_grid, m)

        t0, t1 = tenors[ti],          tenors[ti + 1]
        m0, m1 = moneyness_grid[mi],  moneyness_grid[mi + 1]

        iv00 = bar.Iv[ti][mi]
        iv01 = bar.Iv[ti][mi + 1]
        iv10 = bar.Iv[ti + 1][mi]
        iv11 = bar.Iv[ti + 1][mi + 1]

        wt = (t - t0) / (t1 - t0)
        wm = (m - m0) / (m1 - m0)

        return ((1 - wt) * (1 - wm) * iv00
                + (1 - wt) *      wm  * iv01
                +      wt  * (1 - wm) * iv10
                +      wt  *      wm  * iv11)

    @staticmethod
    def _lower_index(grid, v: float) -> int:
        for i in range(len(grid) - 1):
            if v <= grid[i + 1]:
                return i
        return len(grid) - 2
```

## How it works

`FlashAlphaSurfaceBar` carries the IV grid as a two-dimensional array: `bar.Iv[tenor_index][moneyness_index]`, with the row and column axes labelled by `bar.Tenors` (years) and `bar.Moneyness` (strike / spot). Both axis arrays are equal-length — the side length is `bar.GridSize`. The surface has already been smoothed server-side from the expirations listed in `bar.SlicesUsed`, so it's continuous in both dimensions and safe to interpolate.

The interpolation routine is textbook bilinear:

1. **Clamp** the requested `(tenor, moneyness)` point into the grid. The FlashAlpha surface is calibrated only on the displayed range; extrapolating is misleading. Clamping silently bounces queries off the edge of the grid.
2. **Locate** the lower-left grid cell whose corners bound the request: `(t0, m0)` to `(t1, m1)`.
3. **Bilinearly weight** the four corner IVs by the relative position of the request within the cell.

Both languages share the same algorithm — bilinear interpolation has no language-specific gotchas. The C# version uses `Math.Clamp` and range index syntax (`[^1]`); the Python version uses two-arg `min/max`.

The example uses daily resolution and a single-session backtest range so the algorithm fires `OnData` exactly once. For a research notebook (`lean research`) you can pull the bar via the standard history API without a backtest at all.

## Variations

- **Term-only walk.** Skip the moneyness axis — read `bar.Iv[i][middle]` across `i` to get the ATM term structure.
- **Skew at a fixed tenor.** Fix `tenor_index` and walk `moneyness_index` to extract the smile at one expiry.
- **Total variance.** Multiply each IV² by its tenor before interpolating — interpolating in total-variance space is more arbitrage-safe than IV space for option pricing.
- **Calibrated to a specific strike.** Convert the strike to moneyness with `strike / bar.Spot`, then interpolate. The bar's `Spot` field is the underlying at `AsOf` and is the right denominator.
- **Surface deltas day-over-day.** Subscribe `SurfaceBar` for two dates (or run a backtest over a range) and diff the grids in `OnData` to spot regime changes.
- **Feed an SVI calibration.** Combine with `FlashAlphaAdvVolatilityBar` to get per-expiry SVI parameters (`a, b, ρ, m, σ`) that produce the same surface — the smoothed grid and the SVI params are two views of the same fit.

## Related recipes

- [Subscribe to GEX in QuantConnect](subscribe-to-gex-in-quantconnect.md) — the baseline FlashAlpha subscription pattern.
- [Combine FlashAlpha with equity data](combine-flashalpha-with-equity-data.md) — required if you want spot from LEAN and the surface from FlashAlpha at the same instant.
- [0DTE pin-risk check](0dte-pin-risk-check-in-quantconnect.md) — pairs naturally with a vol-surface read on the front-month wing.
