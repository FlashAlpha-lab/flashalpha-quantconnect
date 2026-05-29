# Getting started with FlashAlpha + QuantConnect

Sixty seconds from install to a running backtest. This guide installs the bridge, wires up an API key, and ships a first algorithm that gates SPY exposure on the FlashAlpha gamma regime. Both C# and Python.

If you already have an API key and a project, skip to [first algorithm](#first-algorithm). For deeper recipes, see [docs/recipes/](recipes/).

---

## Prerequisites

- A **FlashAlpha API key.** Sign up at [flashalpha.com](https://flashalpha.com); free tier covers the basic exposure endpoints. Higher-tier endpoints (`vrp`, `adv-volatility`, `stock/summary`) require an Alpha plan.
- A **QuantConnect environment.** Either:
  - **QC Cloud** — sign in at [quantconnect.com](https://www.quantconnect.com), create a project. The bridge is pre-installed.
  - **Self-hosted LEAN** — install the [LEAN CLI](https://www.lean.io/docs/v2/lean-cli/getting-started/installation), have either the .NET 6+ SDK or Python 3.10+ available depending on which language you use.

---

## Install

### QuantConnect Cloud

No install step. Both `FlashAlpha.QuantConnect` (C#) and `flashalpha-quantconnect` (Python) ship in the QC Cloud project image. Skip ahead to [auth](#configure-the-api-key).

### Self-hosted LEAN — C#

In your algorithm project:

```bash
dotnet add package FlashAlpha.QuantConnect
```

Or in `Project.csproj`:

```xml
<PackageReference Include="FlashAlpha.QuantConnect" Version="0.1.0" />
```

### Self-hosted LEAN — Python

In your LEAN environment:

```bash
pip install flashalpha-quantconnect
```

LEAN's Python runtime picks the package up at the next algorithm start.

---

## Configure the API key

The bridge resolves the FlashAlpha API key in this order:

1. **Explicit override** — `FlashAlphaConfig.ApiKey = "fa_live_…"` in C# / `config.api_key = "fa_live_…"` in Python.
2. **QC Cloud parameter** — `flashalpha-api-key` under **Project → Parameters**.
3. **Environment variable** — `FLASHALPHA_API_KEY`.

If none match the bridge throws `FlashAlphaAuthMissingException` (`FA-AUTH-001`).

### QuantConnect Cloud

1. Open your project.
2. Click **Parameters** in the sidebar.
3. Add a row: name `flashalpha-api-key`, value `fa_live_…` (your key).
4. Save.

The bridge calls `algorithm.GetParameter("flashalpha-api-key")` on first request.

### Self-hosted LEAN — env var

```bash
export FLASHALPHA_API_KEY="fa_live_..."
lean backtest "MyAlgorithm"
```

Or in a `.env` consumed by LEAN. Don't commit the key — see [docs/auth.md](auth.md#secrets-hygiene) for full hygiene rules.

### CI

Store the key as a CI secret (GitHub Actions: `Settings → Secrets and variables → Actions → New repository secret`). Wire it into the env at job time:

```yaml
- name: Run LEAN backtest
  env:
    FLASHALPHA_API_KEY: ${{ secrets.FLASHALPHA_API_KEY }}
  run: lean backtest "MyAlgorithm"
```

---

## First algorithm

Hold SPY one-for-one when the FlashAlpha net dealer gamma label is `"positive"`, flat otherwise. Daily resolution. Both languages.

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

        // Underlying equity — drives PnL.
        _spy = AddEquity("SPY", Resolution.Daily).Symbol;

        // FlashAlpha GEX bar — drives the regime gate.
        // Equivalent to: AddData<FlashAlphaGexBar>("SPY", Resolution.Daily).Symbol.
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

        # Underlying equity — drives PnL.
        self.spy = self.AddEquity("SPY", Resolution.Daily).Symbol

        # FlashAlpha GEX bar — drives the regime gate.
        # Equivalent to: self.AddData(GexBar, "SPY", Resolution.Daily).Symbol.
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

### Run

- **QC Cloud:** click **Backtest** on the project. Look at the equity curve plus the `Debug` lines in the console.
- **Self-hosted LEAN:** `lean backtest "GexRegimeAlgorithm"`.

You should see roughly one debug line per trading day in 2024, each with a populated `net_gex`, label, and gamma-flip strike.

---

## Resolution → API cost

How often the bridge calls FlashAlpha is driven entirely by the LEAN subscription resolution. Pick the lowest resolution your strategy actually needs.

| LEAN resolution     | FlashAlpha calls per ticker per trading day | Notes                                                |
| ------------------- | ------------------------------------------- | ---------------------------------------------------- |
| `Resolution.Daily`  | 1                                           | Default. One end-of-day snapshot.                    |
| `Resolution.Hour`   | ~7                                          | RTH hourly bars (09:30, 10:30, … 15:30 ET).          |
| `Resolution.Minute` | ~390                                        | Heavy. Use for research minute-of-the-day studies.   |
| `Resolution.Tick`   | not supported                               | The historical API isn't tick-granular.              |

A 252-day daily backtest of GEX on a single ticker is 252 API calls. The same backtest at minute resolution is roughly 98,000 calls. The bridge has an in-process cache so duplicate subscriptions sharing `(endpoint, ticker, date)` reuse the response — adding GEX, DEX, and VEX subscriptions on the same date is three calls, not three independent lookups for the same row.

---

## Where to go next

- **The other sixteen bars** — same pattern, different `AddData<…>` (or `add_flashalpha_*`) one-liner. Full field reference: [docs/data-types.md](data-types.md).
- **Combine with equity bars** — pair `AddEquity` with `AddData<FlashAlphaGexBar>` and key by ticker string in `OnData`. Worked example: [docs/recipes/combine-flashalpha-with-equity-data.md](recipes/combine-flashalpha-with-equity-data.md).
- **Multi-ticker universe** — use `FlashAlphaTickersUniverse` to pull the coverage list and gate on healthy-day count or coverage span. Walkthrough: [docs/recipes/filter-universe-by-gex-regime.md](recipes/filter-universe-by-gex-regime.md).
- **0DTE strategies** — read `FlashAlphaZeroDteBar.PinRisk` before entering a same-day-expiry trade: [docs/recipes/0dte-pin-risk-check-in-quantconnect.md](recipes/0dte-pin-risk-check-in-quantconnect.md).
- **Vol-surface arbitrage / interpolation** — pull the SVI grid and read implied vol at an arbitrary `(tenor, moneyness)` point: [docs/recipes/vol-surface-snapshot-in-quantconnect.md](recipes/vol-surface-snapshot-in-quantconnect.md).
- **Something broke** — error codes and diagnostics: [docs/troubleshooting.md](troubleshooting.md).
- **`flashalpha-historical-examples`** (upcoming, separate repo): twenty-plus end-to-end backtest essays using this bridge.
