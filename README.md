# flashalpha-quantconnect

**FlashAlpha options-flow and dealer-positioning data as native QuantConnect LEAN custom-data bars — C# (NuGet) and Python (PyPI).**

Subscribe to GEX (gamma exposure), DEX, VEX, CHEX, the full exposure summary, a smoothed implied-vol surface, 0DTE pin-risk + dealer hedging flows, max-pain, VRP (variance risk premium), advanced volatility (SVI, variance swaps), narrative summaries, stock + option quote books, a coverage universe — seventeen endpoint families — and read them from `OnData` exactly like any other LEAN bar. Backtest dealer-flow strategies on QuantConnect Cloud or self-hosted LEAN with one `AddData<…>` line per data type.

[![NuGet](https://img.shields.io/nuget/v/FlashAlpha.QuantConnect.svg)](https://www.nuget.org/packages/FlashAlpha.QuantConnect)
[![PyPI](https://img.shields.io/pypi/v/flashalpha-quantconnect.svg)](https://pypi.org/project/flashalpha-quantconnect/)
[![CI](https://github.com/FlashAlpha-lab/flashalpha-quantconnect/actions/workflows/ci.yml/badge.svg)](https://github.com/FlashAlpha-lab/flashalpha-quantconnect/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## What it does

`flashalpha-quantconnect` is the official QuantConnect LEAN bridge for the [FlashAlpha historical API](https://historical.flashalpha.com). It plumbs FlashAlpha's options-flow and dealer-positioning analytics into LEAN as first-class custom-data bars so a backtest just calls `AddData<FlashAlphaGexBar>("SPY")` (or `algorithm.AddData(GexBar, "SPY")` in Python) and the bars stream into `OnData` with full per-strike detail — gamma flips, pin probabilities, IV grids, 0DTE Greeks, VRP scores — all timestamped, all replayable, all version-pinned to the underlying SDK so schema drift dies at compile time.

| Family             | Endpoint slug         | C# bar class                          | Python bar class           |
| ------------------ | --------------------- | ------------------------------------- | -------------------------- |
| GEX                | `exposure/gex`        | `FlashAlphaGexBar`                    | `GexBar`                   |
| DEX                | `exposure/dex`        | `FlashAlphaDexBar`                    | `DexBar`                   |
| VEX                | `exposure/vex`        | `FlashAlphaVexBar`                    | `VexBar`                   |
| CHEX               | `exposure/chex`       | `FlashAlphaChexBar`                   | `ChexBar`                  |
| Exposure summary   | `exposure/summary`    | `FlashAlphaExposureSummaryBar`        | `ExposureSummaryBar`       |
| Exposure levels    | `exposure/levels`     | `FlashAlphaExposureLevelsBar`         | `ExposureLevelsBar`        |
| Vol surface        | `surface`             | `FlashAlphaSurfaceBar`                | `SurfaceBar`               |
| Zero-DTE           | `exposure/zero-dte`   | `FlashAlphaZeroDteBar`                | `ZeroDteBar`               |
| Max pain           | `max-pain`            | `FlashAlphaMaxPainBar`                | `MaxPainBar`               |
| Volatility         | `volatility`          | `FlashAlphaVolatilityBar`             | `VolatilityBar`            |
| Advanced vol       | `adv-volatility`      | `FlashAlphaAdvVolatilityBar`          | `AdvVolatilityBar`         |
| VRP                | `vrp`                 | `FlashAlphaVrpBar`                    | `VrpBar`                   |
| Narrative          | `narrative`           | `FlashAlphaNarrativeBar`              | `NarrativeBar`             |
| Stock summary      | `stock/summary`       | `FlashAlphaStockSummaryBar`           | `StockSummaryBar`          |
| Stock quote        | `stock/quote`         | `FlashAlphaStockQuoteBar`             | `StockQuoteBar`            |
| Option quote       | `option/quote`        | `FlashAlphaOptionQuoteBar`            | `OptionQuoteBar`           |
| Tickers (coverage) | `tickers`             | `FlashAlphaTickersBar` + universe     | `TickersBar` + universe    |

Full reference: [docs/data-types.md](docs/data-types.md).

---

## Install

### QuantConnect Cloud (C# or Python)

Both packages are pre-installed in QC Cloud — no `pip install` or NuGet ceremony. Add your API key to **Project → Parameters** with the key `flashalpha-api-key` and paste the value (get one at [flashalpha.com](https://flashalpha.com)). The bridge resolves it via `GetParameter` on the first request.

### Self-hosted LEAN — C# (NuGet)

```bash
dotnet add package FlashAlpha.QuantConnect
```

Or in `.csproj`:

```xml
<PackageReference Include="FlashAlpha.QuantConnect" Version="0.1.0" />
```

Set the key as an env var so LEAN picks it up:

```bash
export FLASHALPHA_API_KEY="fa_live_..."
```

### Self-hosted LEAN — Python (PyPI)

```bash
pip install flashalpha-quantconnect
```

Same env var:

```bash
export FLASHALPHA_API_KEY="fa_live_..."
```

Full auth setup — including CI secrets and key resolution order — lives in [docs/auth.md](docs/auth.md).

---

## QuantConnect GEX — first algorithm

Long when the dealer gamma regime is positive, flat otherwise. Sixty seconds, both languages.

**C#**

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

        _spy = AddEquity("SPY", Resolution.Daily).Symbol;
        _gex = this.AddFlashAlphaGex("SPY").Symbol;
    }

    public override void OnData(Slice slice)
    {
        if (!slice.ContainsKey(_gex)) return;
        var bar = slice.Get<FlashAlphaGexBar>(_gex);
        if (bar == null) return;

        var longRegime = bar.NetGexLabel == "positive";
        SetHoldings(_spy, longRegime ? 1.0m : 0m);
    }
}
```

**Python**

```python
from AlgorithmImports import *
from flashalpha_quantconnect import GexBar, add_flashalpha_gex


class GexRegimeAlgorithm(QCAlgorithm):
    def Initialize(self):
        self.SetStartDate(2024, 1, 1)
        self.SetEndDate(2024, 12, 31)
        self.SetCash(100_000)

        self.spy = self.AddEquity("SPY", Resolution.Daily).Symbol
        self.gex = add_flashalpha_gex(self, "SPY").Symbol

    def OnData(self, slice):
        if self.gex not in slice:
            return
        bar = slice[self.gex]
        long_regime = bar.NetGexLabel == "positive"
        self.SetHoldings(self.spy, 1.0 if long_regime else 0.0)
```

Five more end-to-end recipes — pair-by-ticker with equity bars, 0DTE pin-risk gating, vol-surface snapshots, universe selection by GEX regime — are in [docs/recipes/](docs/recipes/).

---

## Data catalog

Every bar lives at `https://historical.flashalpha.com/docs/<endpoint>`. Full field tables and side-by-side `OnData` samples are in [docs/data-types.md](docs/data-types.md).

> **Tier note:** GEX, levels, and 0DTE bars work on standard tiers. Flow,
> CHEX/VEX (vanna/charm), and point-in-time historical bars require the
> **Alpha tier**. [What Alpha unlocks](https://flashalpha.com/for-quant-teams?utm_source=github&utm_medium=readme&utm_campaign=repo-flashalpha-quantconnect)

| Family             | Endpoint                                                                        | C# class                          | Python class           |
| ------------------ | ------------------------------------------------------------------------------- | --------------------------------- | ---------------------- |
| GEX                | [`exposure/gex`](https://historical.flashalpha.com/docs/exposure/gex)           | `FlashAlphaGexBar`                | `GexBar`               |
| DEX                | [`exposure/dex`](https://historical.flashalpha.com/docs/exposure/dex)           | `FlashAlphaDexBar`                | `DexBar`               |
| VEX                | [`exposure/vex`](https://historical.flashalpha.com/docs/exposure/vex)           | `FlashAlphaVexBar`                | `VexBar`               |
| CHEX               | [`exposure/chex`](https://historical.flashalpha.com/docs/exposure/chex)         | `FlashAlphaChexBar`               | `ChexBar`              |
| Exposure summary   | [`exposure/summary`](https://historical.flashalpha.com/docs/exposure/summary)   | `FlashAlphaExposureSummaryBar`    | `ExposureSummaryBar`   |
| Exposure levels    | [`exposure/levels`](https://historical.flashalpha.com/docs/exposure/levels)     | `FlashAlphaExposureLevelsBar`     | `ExposureLevelsBar`    |
| Vol surface        | [`surface`](https://historical.flashalpha.com/docs/surface)                     | `FlashAlphaSurfaceBar`            | `SurfaceBar`           |
| Zero-DTE           | [`exposure/zero-dte`](https://historical.flashalpha.com/docs/exposure/zero-dte) | `FlashAlphaZeroDteBar`            | `ZeroDteBar`           |
| Max pain           | [`max-pain`](https://historical.flashalpha.com/docs/max-pain)                   | `FlashAlphaMaxPainBar`            | `MaxPainBar`           |
| Volatility         | [`volatility`](https://historical.flashalpha.com/docs/volatility)               | `FlashAlphaVolatilityBar`         | `VolatilityBar`        |
| Advanced vol       | [`adv-volatility`](https://historical.flashalpha.com/docs/adv-volatility)       | `FlashAlphaAdvVolatilityBar`      | `AdvVolatilityBar`     |
| VRP                | [`vrp`](https://historical.flashalpha.com/docs/vrp)                             | `FlashAlphaVrpBar`                | `VrpBar`               |
| Narrative          | [`narrative`](https://historical.flashalpha.com/docs/narrative)                 | `FlashAlphaNarrativeBar`          | `NarrativeBar`         |
| Stock summary      | [`stock/summary`](https://historical.flashalpha.com/docs/stock/summary)         | `FlashAlphaStockSummaryBar`       | `StockSummaryBar`      |
| Stock quote        | [`stock/quote`](https://historical.flashalpha.com/docs/stock/quote)             | `FlashAlphaStockQuoteBar`         | `StockQuoteBar`        |
| Option quote       | [`option/quote`](https://historical.flashalpha.com/docs/option/quote)           | `FlashAlphaOptionQuoteBar`        | `OptionQuoteBar`       |
| Tickers (coverage) | [`tickers`](https://historical.flashalpha.com/docs/tickers)                     | `FlashAlphaTickersBar`            | `TickersBar`           |

Sugar extensions: every bar has a one-liner — `algo.AddFlashAlphaGex("SPY")` in C#, `add_flashalpha_gex(self, "SPY")` in Python — that's equivalent to `AddData<FlashAlphaGexBar>("SPY", Resolution.Daily)` / `self.AddData(GexBar, "SPY", Resolution.Daily)` with the daily default already baked in.

---

## Auth

Get an API key at [flashalpha.com](https://flashalpha.com). The bridge resolves the key in this order:

1. **Explicit override** — `FlashAlphaConfig.ApiKey = "fa_live_…"` in C#, `config.api_key = "fa_live_…"` in Python.
2. **QC Cloud parameter** — set `flashalpha-api-key` under **Project → Parameters**.
3. **Environment variable** — `FLASHALPHA_API_KEY` for self-hosted LEAN.

If all three miss the bridge throws `FlashAlphaAuthMissingException` (error code `FA-AUTH-001`). Full setup — including CI, dotenv, and secrets hygiene — in [docs/auth.md](docs/auth.md).

---

## Resolution → API cost

LEAN resolution drives how often the bridge calls the FlashAlpha API. Pick the lowest resolution that satisfies your strategy.

| LEAN resolution   | FlashAlpha calls per ticker per trading day | Notes                                                 |
| ----------------- | ------------------------------------------- | ----------------------------------------------------- |
| `Resolution.Daily`  | 1                                         | Default. One end-of-day snapshot. Cheapest.           |
| `Resolution.Hour`   | ~7                                        | RTH hourly bars (09:30, 10:30, … 15:30).              |
| `Resolution.Minute` | ~390                                      | Heavy. Use for research minute-of-the-day studies.    |
| `Resolution.Tick`   | not supported                             | The historical API isn't tick-granular.               |

A 252-day daily backtest of GEX on `SPY` is 252 API calls. The same backtest at minute resolution is ~98k calls — same data, just more snapshots per session.

---

## QuantConnect 0DTE — recipes

Five end-to-end recipes, each a copy-paste template:

- [Subscribe to GEX in QuantConnect](docs/recipes/subscribe-to-gex-in-quantconnect.md) — basics.
- [Filter universe by GEX regime](docs/recipes/filter-universe-by-gex-regime.md) — `FlashAlphaTickersUniverse` + per-name GEX gating.
- [Combine FlashAlpha with equity data](docs/recipes/combine-flashalpha-with-equity-data.md) — pair `AddEquity` and `AddData<…>` by ticker string in `OnData`.
- [0DTE pin-risk check in QuantConnect](docs/recipes/0dte-pin-risk-check-in-quantconnect.md) — gate 0DTE entries on dealer-pin score.
- [Vol-surface snapshot in QuantConnect](docs/recipes/vol-surface-snapshot-in-quantconnect.md) — read the IV grid, interpolate at a custom strike/tenor.

---

## FAQ

### Why does FlashAlpha use a separate custom-data Symbol from AddEquity?

QC's custom-data subscription system mints a fresh `Symbol` for each `AddData<T>(ticker, …)` call — distinct from the equity `Symbol` returned by `AddEquity(ticker, …)`. The two are *not interchangeable* in `Slice` lookups. Typical algos hold both symbols as fields and pair them by ticker string in `OnData`. There's a worked example in [docs/recipes/combine-flashalpha-with-equity-data.md](docs/recipes/combine-flashalpha-with-equity-data.md) and a deeper dive at [docs/troubleshooting.md#why-two-symbols](docs/troubleshooting.md#why-two-symbols).

### What's the API cost per backtest day?

One FlashAlpha API call per ticker per bar. At `Resolution.Daily` (the default) that's one call per ticker per trading day — a 252-day SPY-only GEX backtest is 252 calls. At `Resolution.Minute` it's ~390 calls per ticker per day. The bridge has an in-process cache so duplicate subscriptions on the same `(endpoint, ticker, date)` reuse the response.

### Does this work in QC Cloud?

Yes. Both the NuGet and PyPI packages are available in the QC Cloud project environment. Add your API key under **Project → Parameters** with the key `flashalpha-api-key` and the bridge picks it up via `GetParameter`. No env-var setup needed.

### How is this different from polygon/CBOE feeds?

Polygon and CBOE ship the raw chain — quotes, OI, Greeks. FlashAlpha ships the *positioning analytics* derived from that chain: net dealer GEX/DEX/VEX/CHEX, the gamma flip strike, call/put walls, 0DTE pin probability, dealer hedging-flow estimates, the smoothed IV surface and SVI parameters, VRP and harvest scores, plain-English narrative summaries. You can feed FlashAlpha and raw option data into the same algorithm — see [docs/recipes/combine-flashalpha-with-equity-data.md](docs/recipes/combine-flashalpha-with-equity-data.md).

### Can I use this for live trading?

The current bar set targets the *historical* endpoint family — meant for backtests and research, not live signals. The same bars will work live in LEAN when the FlashAlpha realtime endpoints land; the subscription surface won't change. Track [github.com/FlashAlpha-lab/flashalpha-quantconnect/issues](https://github.com/FlashAlpha-lab/flashalpha-quantconnect/issues) for live-mode milestones.

### What's the relationship to the flashalpha-historical SDK?

`flashalpha-historical` is the raw cross-language SDK for the FlashAlpha historical API — available for [Python](https://pypi.org/project/flashalpha-historical/), [.NET](https://www.nuget.org/packages/FlashAlpha.Historical/), [Node](https://www.npmjs.com/package/@flashalpha/historical), [Go](https://pkg.go.dev/github.com/FlashAlpha-lab/flashalpha-historical-go), and [Java](https://central.sonatype.com/artifact/com.flashalpha/flashalpha-historical). `flashalpha-quantconnect` is the *LEAN adapter* on top of it. The bridge's C# bars depend on `FlashAlpha.Historical`'s typed response models so any schema drift in the SDK breaks the build, never silently corrupts a bar. You generally don't import the SDK directly — `AddData<…>` is the user-facing surface.

---

## Related

- [historical.flashalpha.com](https://historical.flashalpha.com) — the underlying API.
- [flashalpha-historical-python](https://pypi.org/project/flashalpha-historical/) / [-dotnet](https://www.nuget.org/packages/FlashAlpha.Historical/) / [-js](https://www.npmjs.com/package/@flashalpha/historical) / [-go](https://pkg.go.dev/github.com/FlashAlpha-lab/flashalpha-historical-go) / [-java](https://central.sonatype.com/artifact/com.flashalpha/flashalpha-historical) — raw SDKs.
- `flashalpha-historical-examples` (upcoming) — twenty-plus end-to-end backtest essays using this bridge.
- [LEAN custom data](https://www.quantconnect.com/docs/v2/writing-algorithms/datasets/custom-data) — QC's reference for how custom-data subscriptions work.

---

## License

MIT. See [LICENSE](LICENSE).

## What the Alpha tier unlocks

Free and entry tiers cover live exposure analytics. The **Alpha tier ($1,499/mo)**
adds the data you cannot get anywhere else:

- **Aggregate vanna and charm exposure.** FlashAlpha is the only public source for
  these dealer-positioning aggregates.
- **Point-in-time replay since 2018.** Backtest and trade the same code, with no
  look-ahead and no training-serving skew.
- **SVI vol surfaces, VRP analytics, higher-order Greeks**, uncached and unlimited.

Built for quants, prop desks, and vol funds. See the full picture and get a key:
**[flashalpha.com/for-quant-teams](https://flashalpha.com/for-quant-teams?utm_source=github&utm_medium=readme&utm_campaign=repo-flashalpha-quantconnect)**
