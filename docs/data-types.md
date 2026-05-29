# Data types

One section per FlashAlpha endpoint family. Each section lists:

- the endpoint slug and a link to the underlying API reference,
- the C# and Python bar class names,
- the field table (every property declared on the bar),
- a representative truncated JSON response,
- a side-by-side `OnData` example.

For top-down navigation jump to the catalog in [README.md](../README.md#data-catalog). For the API-cost-per-resolution table see [getting-started.md](getting-started.md#resolution--api-cost).

Field tables use these conventions:

- **Nullable** — `Yes` means the property is declared as `T?` (C#) or `Optional[T]` (Python) on the bar; null-check before drilling in.
- **Type** — the C# type. Python equivalents follow the obvious mapping (`double?` → `Optional[float]`, `string?` → `str` defaulting to `""`, `List<X>?` → `Optional[List[Any]]`, nested object → `Optional[Dict[str, Any]]` in Python / typed SDK model in C#).
- Wire-format quirks (e.g. JSON key not snake_case) are called out under the field table.

---

## Contents

- [GEX — gamma exposure](#gex--gamma-exposure)
- [DEX — delta exposure](#dex--delta-exposure)
- [VEX — vanna exposure](#vex--vanna-exposure)
- [CHEX — charm exposure](#chex--charm-exposure)
- [Exposure summary](#exposure-summary)
- [Exposure levels](#exposure-levels)
- [Vol surface](#vol-surface)
- [Zero-DTE](#zero-dte)
- [Max pain](#max-pain)
- [Volatility](#volatility)
- [Advanced volatility](#advanced-volatility)
- [VRP — variance risk premium](#vrp--variance-risk-premium)
- [Narrative](#narrative)
- [Stock summary](#stock-summary)
- [Stock quote](#stock-quote)
- [Option quote](#option-quote)
- [Tickers (coverage)](#tickers-coverage)

---

## GEX — gamma exposure

- **Endpoint:** [`exposure/gex`](https://historical.flashalpha.com/docs/exposure/gex)
- **C# class:** `FlashAlphaGexBar`
- **Python class:** `GexBar`
- **Sugar:** `algo.AddFlashAlphaGex("SPY")` / `add_flashalpha_gex(self, "SPY")`

Strike-by-strike net dealer gamma exposure with the headline net GEX, the gamma flip strike, and a coarse `"positive"` / `"negative"` regime label.

### Fields

| Field            | Type                  | Description                                                                                  | Nullable |
| ---------------- | --------------------- | -------------------------------------------------------------------------------------------- | -------- |
| `Ticker`         | `string`              | Ticker echoed by the API (JSON key `symbol`). LEAN's `Symbol` lives on `BaseData`.           | Yes      |
| `UnderlyingPrice`| `double`              | Underlying spot at `AsOf`.                                                                   | Yes      |
| `AsOf`           | `string`              | Server-side timestamp the row was resolved at (ISO-8601).                                    | Yes      |
| `GammaFlip`      | `double`              | Strike where net dealer gamma crosses zero. `null` when no zero crossing exists.             | Yes      |
| `NetGex`         | `double`              | Net dealer gamma exposure, dollars per 1% spot move.                                         | Yes      |
| `NetGexLabel`    | `string`              | Coarse label — typically `"positive"` / `"negative"`.                                        | Yes      |
| `Strikes`        | `List<GexStrikeRow>`  | Per-strike GEX table. Each row carries `Strike`, `CallGex`, `PutGex`, `NetGex`, `CallOi`, `PutOi`, `CallOiChange`, `PutOiChange`. Per-row `CallVolume` / `PutVolume` are placeholders on historical. | Yes |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "gamma_flip": 475.0,
  "net_gex": 1845231231.4,
  "net_gex_label": "positive",
  "strikes": [
    {"strike": 470.0, "call_gex": 81231.2, "put_gex": -213213.0, "net_gex": -132000.0,
     "call_oi": 18200, "put_oi": 24310, "call_oi_change": 1200, "put_oi_change": -300},
    {"strike": 475.0, "call_gex": 142123.1, "put_gex": -141900.2, "net_gex": 222.9,
     "call_oi": 24100, "put_oi": 23800, "call_oi_change": 800, "put_oi_change": 200}
  ]
}
```

### Reading the bar

**C#**

```csharp
public override void OnData(Slice slice)
{
    var bar = slice.Get<FlashAlphaGexBar>(_gex);
    if (bar == null) return;
    Debug($"net_gex={bar.NetGex:F0}  label={bar.NetGexLabel}  flip={bar.GammaFlip:F2}");

    if (bar.Strikes != null)
        foreach (var row in bar.Strikes)
            Debug($"  K={row.Strike:F2}  net={row.NetGex:F0}  callOI={row.CallOi}");
}
```

**Python**

```python
def OnData(self, slice):
    if self.gex not in slice:
        return
    bar = slice[self.gex]
    self.Debug(f"net_gex={bar.NetGex:.0f}  label={bar.NetGexLabel}  flip={bar.GammaFlip:.2f}")

    for row in (bar.Strikes or []):
        self.Debug(f"  K={row['strike']:.2f}  net={row['net_gex']:.0f}  callOI={row['call_oi']}")
```

---

## DEX — delta exposure

- **Endpoint:** [`exposure/dex`](https://historical.flashalpha.com/docs/exposure/dex)
- **C# class:** `FlashAlphaDexBar`
- **Python class:** `DexBar`
- **Sugar:** `algo.AddFlashAlphaDex("SPY")` / `add_flashalpha_dex(self, "SPY")`

Net dealer delta exposure across the chain. Same shape as GEX minus the gamma-flip / label fields — directionally read off `NetDex` for whether dealers are net long or net short the underlier.

### Fields

| Field             | Type                  | Description                                       | Nullable |
| ----------------- | --------------------- | ------------------------------------------------- | -------- |
| `Ticker`          | `string`              | JSON `symbol`.                                    | Yes      |
| `UnderlyingPrice` | `double`              | Underlying spot at `AsOf`.                        | Yes      |
| `AsOf`            | `string`              | ISO-8601 timestamp.                               | Yes      |
| `NetDex`          | `double`              | Net dealer delta exposure (dollars).              | Yes      |
| `Strikes`         | `List<DexStrikeRow>`  | Per-strike DEX table.                             | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "net_dex": -123421000.0,
  "strikes": [
    {"strike": 470.0, "call_dex": 32100.0, "put_dex": -54000.0, "net_dex": -21900.0,
     "call_oi": 18200, "put_oi": 24310}
  ]
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaDexBar>(_dex);
if (bar == null) return;
Debug($"net_dex={bar.NetDex:F0}");
```

**Python**

```python
bar = slice[self.dex]
self.Debug(f"net_dex={bar.NetDex:.0f}")
```

---

## VEX — vanna exposure

- **Endpoint:** [`exposure/vex`](https://historical.flashalpha.com/docs/exposure/vex)
- **C# class:** `FlashAlphaVexBar`
- **Python class:** `VexBar`
- **Sugar:** `algo.AddFlashAlphaVex("SPY")` / `add_flashalpha_vex(self, "SPY")`

Net dealer vanna exposure (∂delta/∂vol). Includes a hand-tuned `VexInterpretation` string that explains the vol-spot linkage in English.

### Fields

| Field                | Type                  | Description                                                          | Nullable |
| -------------------- | --------------------- | -------------------------------------------------------------------- | -------- |
| `Ticker`             | `string`              | JSON `symbol`.                                                       | Yes      |
| `UnderlyingPrice`    | `double`              | Underlying spot at `AsOf`.                                           | Yes      |
| `AsOf`               | `string`              | ISO-8601 timestamp.                                                  | Yes      |
| `NetVex`             | `double`              | Headline net dealer vanna exposure.                                  | Yes      |
| `VexInterpretation`  | `string`              | Plain-English explanation of the regime. Safe to surface verbatim.   | Yes      |
| `Strikes`            | `List<VexStrikeRow>`  | Per-strike VEX table.                                                | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "net_vex": 4123121.0,
  "vex_interpretation": "Dealers are long vanna — falling IV likely supports spot.",
  "strikes": [...]
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaVexBar>(_vex);
if (bar?.VexInterpretation != null) Debug(bar.VexInterpretation);
```

**Python**

```python
bar = slice[self.vex]
if bar.VexInterpretation:
    self.Debug(bar.VexInterpretation)
```

---

## CHEX — charm exposure

- **Endpoint:** [`exposure/chex`](https://historical.flashalpha.com/docs/exposure/chex)
- **C# class:** `FlashAlphaChexBar`
- **Python class:** `ChexBar`
- **Sugar:** `algo.AddFlashAlphaChex("SPY")` / `add_flashalpha_chex(self, "SPY")`

Net dealer charm exposure (∂delta/∂time) — directional delta drift as time passes. `ChexInterpretation` explains the regime.

### Fields

| Field                 | Type                   | Description                                                          | Nullable |
| --------------------- | ---------------------- | -------------------------------------------------------------------- | -------- |
| `Ticker`              | `string`               | JSON `symbol`.                                                       | Yes      |
| `UnderlyingPrice`     | `double`               | Underlying spot at `AsOf`.                                           | Yes      |
| `AsOf`                | `string`               | ISO-8601 timestamp.                                                  | Yes      |
| `NetChex`             | `double`               | Headline net dealer charm exposure.                                  | Yes      |
| `ChexInterpretation`  | `string`               | Plain-English explanation of the regime.                             | Yes      |
| `Strikes`             | `List<ChexStrikeRow>`  | Per-strike CHEX table.                                               | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "net_chex": -812000.0,
  "chex_interpretation": "Dealer delta drifts short as the day decays — supports a fade.",
  "strikes": [...]
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaChexBar>(_chex);
if (bar == null) return;
Debug($"net_chex={bar.NetChex:F0}");
```

**Python**

```python
bar = slice[self.chex]
self.Debug(f"net_chex={bar.NetChex:.0f}")
```

---

## Exposure summary

- **Endpoint:** [`exposure/summary`](https://historical.flashalpha.com/docs/exposure/summary)
- **C# class:** `FlashAlphaExposureSummaryBar`
- **Python class:** `ExposureSummaryBar`
- **Sugar:** `algo.AddFlashAlphaExposureSummary("SPY")` / `add_flashalpha_exposure_summary(self, "SPY")`

The roll-up of GEX/DEX/VEX/CHEX into a single response with an English `Regime` label, `HedgingEstimate` for ±1% spot moves, and a 0DTE contribution block. Use this when you only need the headline — one call vs four.

### Fields

| Field             | Type                                 | Description                                                                              | Nullable |
| ----------------- | ------------------------------------ | ---------------------------------------------------------------------------------------- | -------- |
| `Ticker`          | `string`                             | JSON `symbol`.                                                                           | Yes      |
| `UnderlyingPrice` | `double`                             | Underlying spot at `AsOf`.                                                               | Yes      |
| `AsOf`            | `string`                             | ISO-8601 timestamp.                                                                      | Yes      |
| `GammaFlip`       | `double`                             | Strike where net dealer gamma crosses zero.                                              | Yes      |
| `Regime`          | `string`                             | One of `"positive_gamma"`, `"negative_gamma"`, or `"unknown"`.                           | Yes      |
| `Exposures`       | `ExposureSummaryExposures` / `dict`  | Net totals across the chain.                                                             | Yes      |
| `Interpretation`  | `ExposureSummaryInterpretation` / `dict` | Verbal interpretation of the gamma/vanna/charm regimes.                              | Yes      |
| `HedgingEstimate` | `ExposureSummaryHedgingEstimate` / `dict` | Estimated dealer hedging flow at ±1% spot moves.                                   | Yes      |
| `ZeroDte`         | `ExposureSummaryZeroDte` / `dict`    | Same-day-expiration contribution to total GEX.                                           | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "gamma_flip": 475.0,
  "regime": "positive_gamma",
  "exposures": {"net_gex": 1845231231.4, "net_dex": -123421000.0, "net_vex": 4123121.0, "net_chex": -812000.0},
  "interpretation": {"gamma": "Dealers long gamma; expect mean reversion.", "vanna": "...", "charm": "..."},
  "hedging_estimate": {"up_1pct": -42000000.0, "down_1pct": 51000000.0},
  "zero_dte": {"gex_share": 0.18}
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaExposureSummaryBar>(_summary);
if (bar?.Exposures == null) return;
Debug($"regime={bar.Regime}  net_gex={bar.Exposures.NetGex:F0}  hedging↑1%={bar.HedgingEstimate?.Up1pct:F0}");
```

**Python**

```python
bar = slice[self.summary]
ex = bar.Exposures or {}
hedge = bar.HedgingEstimate or {}
self.Debug(f"regime={bar.Regime}  net_gex={ex.get('net_gex'):.0f}  hedging↑1%={hedge.get('up_1pct'):.0f}")
```

---

## Exposure levels

- **Endpoint:** [`exposure/levels`](https://historical.flashalpha.com/docs/exposure/levels)
- **C# class:** `FlashAlphaExposureLevelsBar`
- **Python class:** `ExposureLevelsBar`
- **Sugar:** `algo.AddFlashAlphaExposureLevels("SPY")` / `add_flashalpha_exposure_levels(self, "SPY")`

The distilled set of dealer-flow key levels — gamma flip, call wall, put wall, max GEX strike, magnet, etc. Skip the per-strike detail and read the trade-able levels directly.

### Fields

| Field             | Type                      | Description                                                                  | Nullable |
| ----------------- | ------------------------- | ---------------------------------------------------------------------------- | -------- |
| `Ticker`          | `string`                  | JSON `symbol`.                                                               | Yes      |
| `UnderlyingPrice` | `double`                  | Underlying spot at `AsOf`.                                                   | Yes      |
| `AsOf`            | `string`                  | ISO-8601 timestamp.                                                          | Yes      |
| `Levels`          | `ExposureLevels` / `dict` | Key levels block — drill in for `gamma_flip`, `call_wall`, `put_wall`, etc.  | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "levels": {
    "gamma_flip": 475.0,
    "call_wall": 480.0,
    "put_wall": 470.0,
    "max_gex_strike": 478.0,
    "magnet": 477.5
  }
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaExposureLevelsBar>(_levels);
if (bar?.Levels == null) return;
Debug($"flip={bar.Levels.GammaFlip:F2}  callWall={bar.Levels.CallWall:F2}  putWall={bar.Levels.PutWall:F2}");
```

**Python**

```python
bar = slice[self.levels]
lv = bar.Levels or {}
self.Debug(f"flip={lv.get('gamma_flip')}  callWall={lv.get('call_wall')}  putWall={lv.get('put_wall')}")
```

---

## Vol surface

- **Endpoint:** [`surface`](https://historical.flashalpha.com/docs/surface)
- **C# class:** `FlashAlphaSurfaceBar`
- **Python class:** `SurfaceBar`
- **Sugar:** `algo.AddFlashAlphaSurface("SPY")` / `add_flashalpha_surface(self, "SPY")`

A smoothed implied-vol surface — a tenor × moneyness grid of IVs. Walk the grid or interpolate at an arbitrary point.

### Fields

| Field         | Type                | Description                                                                      | Nullable |
| ------------- | ------------------- | -------------------------------------------------------------------------------- | -------- |
| `Ticker`      | `string`            | JSON `symbol`.                                                                   | Yes      |
| `Spot`        | `double`            | Spot price at `AsOf` (note: JSON key is `spot`, not `underlying_price`).         | Yes      |
| `AsOf`        | `string`            | ISO-8601 timestamp.                                                              | Yes      |
| `GridSize`    | `int`               | Side length — both `Tenors` and `Moneyness` have this many entries.              | Yes      |
| `Tenors`      | `double[]`          | Tenor values (years) — rows of `Iv`.                                             | Yes      |
| `Moneyness`   | `double[]`          | Moneyness values — columns of `Iv`.                                              | Yes      |
| `Iv`          | `double[][]`        | IV grid. `Iv[tenorIndex][moneynessIndex]`. Annualised, percent.                  | Yes      |
| `SlicesUsed`  | `List<string>`      | Expirations that contributed to the smoothed surface.                            | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "spot": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "grid_size": 5,
  "tenors": [0.02, 0.08, 0.25, 0.5, 1.0],
  "moneyness": [0.9, 0.95, 1.0, 1.05, 1.1],
  "iv": [
    [0.34, 0.27, 0.18, 0.15, 0.21],
    [0.31, 0.24, 0.17, 0.16, 0.20],
    [0.28, 0.22, 0.16, 0.17, 0.21]
  ],
  "slices_used": ["2024-06-17", "2024-06-21", "2024-07-19"]
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaSurfaceBar>(_surface);
if (bar?.Iv == null) return;
// ATM at the front tenor:
var atmFront = bar.Iv[0][bar.Moneyness!.Length / 2];
Debug($"front ATM IV={atmFront:F3}");
```

**Python**

```python
bar = slice[self.surface]
if not bar.Iv:
    return
atm_front = bar.Iv[0][len(bar.Moneyness) // 2]
self.Debug(f"front ATM IV={atm_front:.3f}")
```

There's an interpolation recipe in [docs/recipes/vol-surface-snapshot-in-quantconnect.md](recipes/vol-surface-snapshot-in-quantconnect.md).

---

## Zero-DTE

- **Endpoint:** [`exposure/zero-dte`](https://historical.flashalpha.com/docs/exposure/zero-dte)
- **C# class:** `FlashAlphaZeroDteBar`
- **Python class:** `ZeroDteBar`
- **Sugar:** `algo.AddFlashAlphaZeroDte("SPY")` / `add_flashalpha_zero_dte(self, "SPY")`

The single richest bar in the bridge. Same-day-expiry dealer positioning: regime, exposures, expected move, pin risk, hedging flows, decay, vol context, flow, key levels, liquidity, plus per-strike breakdown. On names with no same-day expiry the bar arrives "thin" — `NoZeroDte = true`, all nested blocks null, `NextZeroDteExpiry` pointing forward.

### Fields

| Field                  | Type                            | Description                                                                                       | Nullable |
| ---------------------- | ------------------------------- | ------------------------------------------------------------------------------------------------- | -------- |
| `Ticker`               | `string`                        | JSON `symbol`.                                                                                    | Yes      |
| `UnderlyingPrice`      | `double`                        | Underlying spot at `AsOf`.                                                                        | Yes      |
| `Expiration`           | `string`                        | Same-day expiration date (YYYY-MM-DD).                                                            | Yes      |
| `AsOf`                 | `string`                        | ISO-8601 timestamp.                                                                               | Yes      |
| `MarketOpen`           | `bool`                          | `true` when resolved during the US-equity session.                                                | Yes      |
| `TimeToCloseHours`     | `double`                        | Hours remaining until cash close.                                                                 | Yes      |
| `TimeToClosePct`       | `double`                        | Fraction of session ahead — 1.0 at open, 0.0 at close.                                            | Yes      |
| `Regime`               | `ZeroDteRegime` / `dict`        | Regime label + gamma-flip context.                                                                | Yes      |
| `Exposures`            | `ZeroDteExposures` / `dict`     | 0DTE net Greek exposures plus share-of-total-chain GEX.                                           | Yes      |
| `ExpectedMove`         | `ZeroDteExpectedMove` / `dict`  | Implied 1-sigma move (full session, remaining) and ATM IV.                                        | Yes      |
| `PinRisk`              | `ZeroDtePinRisk` / `dict`       | Magnet strike, pin score, component breakdown.                                                    | Yes      |
| `Hedging`              | `ZeroDteHedging` / `dict`       | Dealer hedging flow estimates across a grid of spot moves.                                        | Yes      |
| `Decay`                | `ZeroDteDecay` / `dict`         | Theta and charm regime for the remaining session.                                                 | Yes      |
| `VolContext`           | `ZeroDteVolContext` / `dict`    | 0DTE vs 7DTE ATM IV, VIX, vanna exposure.                                                         | Yes      |
| `Flow`                 | `ZeroDteFlow` / `dict`          | 0DTE volume / OI breakdown and put-call ratios.                                                   | Yes      |
| `Levels`               | `ZeroDteLevels` / `dict`        | 0DTE-specific dealer-flow key levels.                                                             | Yes      |
| `Liquidity`            | `ZeroDteLiquidity` / `dict`     | Spread + execution-score liquidity metrics.                                                       | Yes      |
| `Metadata`             | `ZeroDteMetadata` / `dict`      | Snapshot age and data-quality scores.                                                             | Yes      |
| `Strikes`              | `List<ZeroDteStrike>`           | Per-strike 0DTE breakdown — full Greek + flow snapshot.                                           | Yes      |
| `Warnings`             | `List<string>`                  | Non-fatal warnings the engine emitted.                                                            | Yes      |
| `NoZeroDte`            | `bool`                          | `true` when no same-day expiry exists for the ticker.                                             | Yes      |
| `Message`              | `string`                        | Human-readable message accompanying a "no zero-DTE" response.                                     | Yes      |
| `NextZeroDteExpiry`    | `string`                        | Next available expiry when `NoZeroDte` is `true`.                                                 | Yes      |

### Sample response (populated)

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "expiration": "2024-06-14",
  "as_of": "2024-06-14T17:00:00Z",
  "market_open": true,
  "time_to_close_hours": 3.0,
  "time_to_close_pct": 0.46,
  "regime": {"label": "positive_gamma", "gamma_flip": 477.5},
  "pin_risk": {"score": 72, "magnet_strike": 478.0, "components": {"gex": 0.4, "oi": 0.3, "distance": 0.3}},
  "expected_move": {"sigma_full": 4.1, "sigma_remaining": 2.0, "atm_iv": 0.12},
  ...
}
```

### Sample response (thin)

```json
{
  "symbol": "AAPL",
  "no_zero_dte": true,
  "message": "AAPL does not have a same-day expiration; next is 2024-06-21.",
  "next_zero_dte_expiry": "2024-06-21"
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaZeroDteBar>(_zdte);
if (bar == null) return;
if (bar.NoZeroDte == true) { Debug($"no 0DTE — next {bar.NextZeroDteExpiry}"); return; }
Debug($"pin_score={bar.PinRisk?.Score}  magnet={bar.PinRisk?.MagnetStrike:F2}");
```

**Python**

```python
bar = slice[self.zdte]
if bar.NoZeroDte:
    self.Debug(f"no 0DTE — next {bar.NextZeroDteExpiry}")
    return
pr = bar.PinRisk or {}
self.Debug(f"pin_score={pr.get('score')}  magnet={pr.get('magnet_strike')}")
```

Full pin-risk recipe: [docs/recipes/0dte-pin-risk-check-in-quantconnect.md](recipes/0dte-pin-risk-check-in-quantconnect.md).

---

## Max pain

- **Endpoint:** [`max-pain`](https://historical.flashalpha.com/docs/max-pain)
- **C# class:** `FlashAlphaMaxPainBar`
- **Python class:** `MaxPainBar`
- **Sugar:** `algo.AddFlashAlphaMaxPain("SPY")` / `add_flashalpha_max_pain(self, "SPY")`

The strike that minimizes aggregate option-writer pain, plus distance-to-spot, dealer-wall alignment, pin probability score, expected move check, and the full pain curve.

### Fields

| Field                  | Type                                  | Description                                                                                | Nullable |
| ---------------------- | ------------------------------------- | ------------------------------------------------------------------------------------------ | -------- |
| `Ticker`               | `string`                              | JSON `symbol`.                                                                             | Yes      |
| `UnderlyingPrice`      | `double`                              | Underlying spot at `AsOf`.                                                                 | Yes      |
| `AsOf`                 | `string`                              | ISO-8601 timestamp.                                                                        | Yes      |
| `MaxPainStrike`        | `double`                              | Strike where total writer pain is minimized.                                               | Yes      |
| `Distance`             | `MaxPainDistance` / `dict`            | Distance spot → pain (absolute, percent, direction).                                       | Yes      |
| `Signal`               | `string`                              | Coarse pin/magnet signal (e.g. `"pin"`, `"gravitate"`).                                    | Yes      |
| `Expiration`           | `string`                              | Single-expiry mode: the expiration. Null when `MaxPainByExpiration` is set.                | Yes      |
| `PutCallOiRatio`       | `double`                              | Put OI ÷ call OI across the request's expiry scope.                                        | Yes      |
| `PainCurve`            | `List<MaxPainCurveRow>`               | Strike-by-strike pain curve. Minimum is at `MaxPainStrike`.                                | Yes      |
| `OiByStrike`           | `List<MaxPainOiRow>`                  | Per-strike OI + volume breakdown.                                                          | Yes      |
| `MaxPainByExpiration`  | `List<MaxPainByExpirationRow>`        | Roll-up-all-expiries mode: one row per expiry. Null when `Expiration` is set.              | Yes      |
| `DealerAlignment`      | `MaxPainDealerAlignment` / `dict`     | Whether dealer-flow walls align with the pain strike.                                      | Yes      |
| `Regime`               | `string`                              | Coarse regime (e.g. `"calm"`, `"trending"`).                                               | Yes      |
| `ExpectedMove`         | `MaxPainExpectedMove` / `dict`        | Expected-move bounds and whether pain falls inside them.                                   | Yes      |
| `PinProbability`       | `int`                                 | Composite 0-100 score for the likelihood spot pins.                                        | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "max_pain_strike": 477.0,
  "distance": {"abs": 1.21, "pct": 0.0025, "direction": "below"},
  "signal": "gravitate",
  "expiration": "2024-06-21",
  "put_call_oi_ratio": 1.14,
  "regime": "calm",
  "pin_probability": 64,
  "pain_curve": [{"strike": 470.0, "pain": 18200000.0}, ...]
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaMaxPainBar>(_mp);
if (bar == null) return;
Debug($"pain={bar.MaxPainStrike:F2}  prob={bar.PinProbability}  signal={bar.Signal}");
```

**Python**

```python
bar = slice[self.mp]
self.Debug(f"pain={bar.MaxPainStrike:.2f}  prob={bar.PinProbability}  signal={bar.Signal}")
```

---

## Volatility

- **Endpoint:** [`volatility`](https://historical.flashalpha.com/docs/volatility)
- **C# class:** `FlashAlphaVolatilityBar`
- **Python class:** `VolatilityBar`
- **Sugar:** `algo.AddFlashAlphaVolatility("SPY")` / `add_flashalpha_volatility(self, "SPY")`

The full volatility analytics block: realized vol ladder, IV-RV spreads, per-expiry skew profiles, term structure, IV dispersion, GEX/theta by DTE, put-call profile, OI concentration, hedging scenarios, and liquidity.

### Fields

| Field                | Type                                       | Description                                                                              | Nullable |
| -------------------- | ------------------------------------------ | ---------------------------------------------------------------------------------------- | -------- |
| `Ticker`             | `string`                                   | JSON `symbol`.                                                                           | Yes      |
| `UnderlyingPrice`    | `double`                                   | Underlying spot at `AsOf`.                                                               | Yes      |
| `AsOf`               | `string`                                   | ISO-8601 timestamp.                                                                      | Yes      |
| `MarketOpen`         | `bool`                                     | `true` when resolved during the US-equity session.                                       | Yes      |
| `RealizedVol`        | `VolatilityRealizedVol` / `dict`           | 5d / 10d / 20d / 30d / 60d realized vol (annualised %).                                  | Yes      |
| `AtmIv`              | `double`                                   | At-the-money implied volatility (annualised %). Top-level scalar, not under `IvRvSpreads`. | Yes    |
| `IvRvSpreads`        | `VolatilityIvRvSpreads` / `dict`           | IV-RV spreads across 5d / 10d / 20d / 30d horizons.                                      | Yes      |
| `SkewProfiles`       | `List<VolatilitySkewProfile>`              | Per-expiry skew: 10Δ / 25Δ wings, ATM, smile ratio, tail convexity.                      | Yes      |
| `TermStructure`      | `VolatilityTermStructure` / `dict`         | Near vs far slope, contango/backwardation state.                                         | Yes      |
| `IvDispersion`       | `VolatilityIvDispersion` / `dict`          | IV dispersion across expiries and strikes.                                               | Yes      |
| `GexByDte`           | `List<VolatilityGexByDte>`                 | Net dealer GEX aggregated by DTE bucket.                                                 | Yes      |
| `ThetaByDte`         | `List<VolatilityThetaByDte>`               | Net option theta aggregated by DTE bucket.                                               | Yes      |
| `PutCallProfile`     | `VolatilityPutCallProfile` / `dict`        | By-expiry OI/volume ratios + by-moneyness OI breakdown.                                  | Yes      |
| `OiConcentration`    | `VolatilityOiConcentration` / `dict`       | Top-3/5/10% share + Herfindahl index.                                                    | Yes      |
| `HedgingScenarios`   | `List<VolatilityHedgingScenario>`          | Projected dealer rebalance + notional at ±X% spot moves.                                 | Yes      |
| `Liquidity`          | `VolatilityLiquidity` / `dict`             | Bid-ask liquidity at ATM and wing regions.                                               | Yes      |

### Sample response

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "market_open": false,
  "realized_vol": {"rv_5d": 0.082, "rv_10d": 0.094, "rv_20d": 0.103, "rv_30d": 0.108, "rv_60d": 0.114},
  "atm_iv": 0.121,
  "iv_rv_spreads": {"vrp_5d": 0.039, "vrp_10d": 0.027, ...},
  "term_structure": {"slope": 0.012, "state": "contango"}
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaVolatilityBar>(_vol);
if (bar == null) return;
Debug($"atm_iv={bar.AtmIv:F3}  rv_20d={bar.RealizedVol?.Rv20d:F3}");
```

**Python**

```python
bar = slice[self.vol]
rv = bar.RealizedVol or {}
self.Debug(f"atm_iv={bar.AtmIv:.3f}  rv_20d={rv.get('rv_20d'):.3f}")
```

---

## Advanced volatility

- **Endpoint:** [`adv-volatility`](https://historical.flashalpha.com/docs/adv-volatility)
- **C# class:** `FlashAlphaAdvVolatilityBar`
- **Python class:** `AdvVolatilityBar`
- **Sugar:** `algo.AddFlashAlphaAdvVolatility("SPY")` / `add_flashalpha_adv_volatility(self, "SPY")`

Per-expiry SVI parameters, forward prices, the full total-variance surface (log-moneyness × tenor grid), arbitrage flags, variance-swap fair values, and second-/third-order Greek surfaces.

**Plan tier:** Alpha or above. **Cold-cache latency:** ~1.5s — bump `HttpTimeout` if you see timeouts.

### Fields

| Field                       | Type                                        | Description                                                                              | Nullable |
| --------------------------- | ------------------------------------------- | ---------------------------------------------------------------------------------------- | -------- |
| `Ticker`                    | `string`                                    | JSON `symbol`.                                                                           | Yes      |
| `UnderlyingPrice`           | `double`                                    | Underlying spot at `AsOf`.                                                               | Yes      |
| `AsOf`                      | `string`                                    | ISO-8601 timestamp.                                                                      | Yes      |
| `MarketOpen`                | `bool`                                      | `true` during the US-equity session.                                                     | Yes      |
| `SviParameters`             | `List<AdvVolatilitySviParams>`              | Per-expiry SVI: (a, b, ρ, m, σ) + forward + ATM total variance.                          | Yes      |
| `ForwardPrices`             | `List<AdvVolatilityForwardPrice>`           | Per-expiry forward prices and basis vs spot.                                             | Yes      |
| `TotalVarianceSurface`      | `AdvVolatilityVarianceSurface` / `dict`     | Log-moneyness × tenor grid + implied-vol grid.                                           | Yes      |
| `ArbitrageFlags`            | `List<AdvVolatilityArbitrageFlag>`          | Detected butterfly / calendar arbitrage violations.                                      | Yes      |
| `VarianceSwapFairValues`    | `List<AdvVolatilityVarianceSwap>`           | Variance-swap fair values per expiry, with convexity adjustment.                         | Yes      |
| `GreeksSurfaces`            | `AdvVolatilityGreeksSurfaces` / `dict`      | Second-/third-order greek surfaces (vanna, charm, volga, speed).                         | Yes      |

### Sample response (truncated)

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "svi_parameters": [
    {"expiry": "2024-06-21", "a": 0.0034, "b": 0.082, "rho": -0.41, "m": -0.02, "sigma": 0.12,
     "forward": 478.4, "atm_total_variance": 0.0021}
  ],
  "arbitrage_flags": [],
  "variance_swap_fair_values": [{"expiry": "2024-06-21", "fair_value": 0.119, "convexity_adj": 0.004}]
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaAdvVolatilityBar>(_advvol);
if (bar?.SviParameters == null) return;
foreach (var p in bar.SviParameters)
    Debug($"  {p.Expiry}  forward={p.Forward:F2}  atm_var={p.AtmTotalVariance:F4}");
```

**Python**

```python
bar = slice[self.advvol]
for p in (bar.SviParameters or []):
    self.Debug(f"  {p['expiry']}  forward={p['forward']:.2f}  atm_var={p['atm_total_variance']:.4f}")
```

---

## VRP — variance risk premium

- **Endpoint:** [`vrp`](https://historical.flashalpha.com/docs/vrp)
- **C# class:** `FlashAlphaVrpBar`
- **Python class:** `VrpBar`
- **Sugar:** `algo.AddFlashAlphaVrp("SPY")` / `add_flashalpha_vrp(self, "SPY")`

Variance-risk-premium analytics — the spread between implied and realized vol, conditioned on regime, with strategy suitability scores. **Plan tier:** Alpha or above.

Common silent-null traps:

- `ZScore` / `Percentile` live on `Vrp`, **not the top level**.
- `NetGex` lives on `Regime`, **not the top level**.
- `HarvestScore` (top-level concept in the docs) is `GexConditioned.HarvestScore`; `NetHarvestScore` is a separate composite.
- `StrategyScores` and `NetHarvestScore` can be `null` on early historical timestamps — check `Warnings`.

### Fields

| Field                  | Type                                  | Description                                                                                       | Nullable |
| ---------------------- | ------------------------------------- | ------------------------------------------------------------------------------------------------- | -------- |
| `Ticker`               | `string`                              | JSON `symbol`.                                                                                    | Yes      |
| `UnderlyingPrice`      | `double`                              | Underlying spot at `AsOf`.                                                                        | Yes      |
| `AsOf`                 | `string`                              | ISO-8601 timestamp.                                                                               | Yes      |
| `MarketOpen`           | `bool`                                | `true` during the US-equity session.                                                              | Yes      |
| `Vrp`                  | `VrpCore` / `dict`                    | Core VRP metrics block. `ZScore` / `Percentile` live here.                                        | Yes      |
| `VarianceRiskPremium`  | `double`                              | Headline VRP scalar.                                                                              | Yes      |
| `ConvexityPremium`     | `double`                              | Convexity premium.                                                                                | Yes      |
| `FairVol`              | `double`                              | Fair-vol estimate.                                                                                | Yes      |
| `Directional`          | `VrpDirectional` / `dict`             | Directional VRP skew (downside/upside wings).                                                     | Yes      |
| `TermVrp`              | `List<VrpTermItem>`                   | VRP term structure — one row per DTE bucket.                                                      | Yes      |
| `GexConditioned`       | `VrpGexConditioned` / `dict`          | VRP harvest score conditioned on dealer-gamma regime.                                             | Yes      |
| `VannaConditioned`     | `VrpVannaConditioned` / `dict`        | VRP outlook conditioned on net dealer vanna.                                                      | Yes      |
| `Regime`               | `VrpRegime` / `dict`                  | Regime snapshot. `NetGex` lives here.                                                             | Yes      |
| `StrategyScores`       | `VrpStrategyScores` / `dict`          | 0-100 strategy suitability scores. Null on early historical timestamps.                           | Yes      |
| `NetHarvestScore`      | `int`                                 | 0-100 composite harvest signal. Null on early historical timestamps.                              | Yes      |
| `DealerFlowRisk`       | `int`                                 | Dealer-flow risk score.                                                                           | Yes      |
| `Warnings`             | `List<string>`                        | Server-side warnings about data quality. Always present (possibly empty).                         | Yes      |
| `Macro`                | `VrpMacro` / `dict`                   | Macro-context snapshot used to condition the VRP outlook.                                         | Yes      |

### Sample response (truncated)

```json
{
  "symbol": "SPY",
  "as_of": "2024-06-14T20:00:00Z",
  "vrp": {"z_score": 0.62, "percentile": 78, "spread_20d": 0.027},
  "variance_risk_premium": 0.027,
  "regime": {"net_gex": 1845231231.4, "label": "positive_gamma"},
  "strategy_scores": {"short_straddle": 71, "iron_condor": 64, "short_vol": 68},
  "net_harvest_score": 67,
  "warnings": []
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaVrpBar>(_vrp);
if (bar == null) return;
var z = bar.Vrp?.ZScore;          // careful — ZScore is on Vrp, not the top level
var netGex = bar.Regime?.NetGex;  // NetGex is on Regime
Debug($"vrp={bar.VarianceRiskPremium:F3}  z={z:F2}  harvest={bar.NetHarvestScore}  netGex={netGex:F0}");
```

**Python**

```python
bar = slice[self.vrp]
vrp = bar.Vrp or {}
regime = bar.Regime or {}
self.Debug(
    f"vrp={bar.VarianceRiskPremium:.3f}  z={vrp.get('z_score'):.2f}  "
    f"harvest={bar.NetHarvestScore}  netGex={regime.get('net_gex'):.0f}"
)
```

---

## Narrative

- **Endpoint:** [`narrative`](https://historical.flashalpha.com/docs/narrative)
- **C# class:** `FlashAlphaNarrativeBar`
- **Python class:** `NarrativeBar`
- **Sugar:** `algo.AddFlashAlphaNarrative("SPY")` / `add_flashalpha_narrative(self, "SPY")`

A hand-tuned, numbers-aware prose summary of the day's dealer positioning — regime, GEX change, key levels, flow, vanna, charm, 0DTE, outlook — plus the raw numbers backing each sentence under `Narrative.data`. Useful as an LLM prompt context block or a human-readable log line.

### Fields

| Field             | Type                       | Description                                                                                       | Nullable |
| ----------------- | -------------------------- | ------------------------------------------------------------------------------------------------- | -------- |
| `Ticker`          | `string`                   | JSON `symbol`.                                                                                    | Yes      |
| `UnderlyingPrice` | `double`                   | Underlying spot at `AsOf`.                                                                        | Yes      |
| `AsOf`            | `string`                   | UTC timestamp the API actually used — snapped to the available minute.                            | Yes      |
| `Narrative`       | `NarrativeBlock` / `dict`  | Prose lines under keys `regime` / `gex_change` / `key_levels` / `flow` / `vanna` / `charm` / `zero_dte` / `outlook`, plus a `data` sub-block with the raw numbers. | Yes |

### Sample response (truncated)

```json
{
  "symbol": "SPY",
  "underlying_price": 478.21,
  "as_of": "2024-06-14T20:00:00Z",
  "narrative": {
    "regime": "Dealers are net long $1.85B of gamma; expect mean-reverting tape.",
    "key_levels": "Gamma flip 475, call wall 480, put wall 470.",
    "outlook": "Calm session likely; pin score 72 around 478.",
    "data": {"net_gex": 1845231231, "gamma_flip": 475.0, "call_wall": 480.0, "put_wall": 470.0}
  }
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaNarrativeBar>(_narr);
if (bar?.Narrative == null) return;
Debug(bar.Narrative.Regime);
Debug(bar.Narrative.Outlook);
```

**Python**

```python
bar = slice[self.narr]
n = bar.Narrative or {}
self.Debug(n.get("regime", ""))
self.Debug(n.get("outlook", ""))
```

---

## Stock summary

- **Endpoint:** [`stock/summary`](https://historical.flashalpha.com/docs/stock/summary)
- **C# class:** `FlashAlphaStockSummaryBar`
- **Python class:** `StockSummaryBar`
- **Sugar:** `algo.AddFlashAlphaStockSummary("SPY")` / `add_flashalpha_stock_summary(self, "SPY")`

The "single best snapshot" composite endpoint — price + volatility + options flow + exposure + macro context. Use this when one bar should give you the whole picture for a name. **Plan tier:** Alpha or above.

**Historical-specific gaps:**

- `OptionsFlow.TotalCallVolume` / `TotalPutVolume` / `PcRatioVolume` are always 0 / null (no minute volume on replay).
- `Macro.VixFutures` is always null (CME futures aren't historically reconstructible from minute data).
- `Macro.FearAndGreed` is always null (the CNN index isn't archived).

### Fields

| Field          | Type                                | Description                                                                                | Nullable |
| -------------- | ----------------------------------- | ------------------------------------------------------------------------------------------ | -------- |
| `Ticker`       | `string`                            | JSON `symbol`.                                                                             | Yes      |
| `AsOf`         | `string`                            | UTC timestamp snapped to the available minute.                                             | Yes      |
| `MarketOpen`   | `bool`                              | `true` during the US-equity session.                                                       | Yes      |
| `PriceQuote`   | `StockSummaryPrice` / `dict`        | Top-of-book block: bid / ask / mid / last + last-update. Renamed from SDK `price` to avoid colliding with `BaseData.Price`. | Yes |
| `Volatility`   | `StockSummaryVolatility` / `dict`   | ATM IV, HV20/60, VRP, 25-delta skew, IV term structure.                                    | Yes      |
| `OptionsFlow`  | `StockSummaryOptionsFlow` / `dict`  | Aggregate options-flow stats — OI/volume by side + put-call ratios.                        | Yes      |
| `Exposure`     | `StockSummaryExposure` / `dict`     | Dealer-exposure block — greeks, walls, gamma flip, max pain, hedging, 0DTE, top strikes.   | Yes      |
| `Macro`        | `StockSummaryMacro` / `dict`        | VIX / VVIX / SKEW / SPX / MOVE + term structure + fear-and-greed.                          | Yes      |

### Sample response (truncated)

```json
{
  "symbol": "SPY",
  "as_of": "2024-06-14T20:00:00Z",
  "market_open": false,
  "price": {"bid": 478.20, "ask": 478.22, "mid": 478.21, "last": 478.21, "last_update": "2024-06-14T19:59:57Z"},
  "volatility": {"atm_iv": 0.121, "hv_20": 0.103, "hv_60": 0.114, "vrp": 0.018, "skew_25d": 0.024},
  "exposure": {"net_gex": 1845231231.4, "gamma_flip": 475.0, "call_wall": 480.0, "put_wall": 470.0},
  "macro": {"vix": 12.4, "vvix": 78.2, "skew": 142.1, "vix_futures": null}
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaStockSummaryBar>(_sum);
if (bar == null) return;
var mid = bar.PriceQuote?.Mid;
var atmIv = bar.Volatility?.AtmIv;
var flip = bar.Exposure?.GammaFlip;
Debug($"mid={mid:F2}  atm_iv={atmIv:F3}  flip={flip:F2}");
```

**Python**

```python
bar = slice[self.sum]
pq = bar.PriceQuote or {}
vol = bar.Volatility or {}
ex = bar.Exposure or {}
self.Debug(f"mid={pq.get('mid')}  atm_iv={vol.get('atm_iv')}  flip={ex.get('gamma_flip')}")
```

---

## Stock quote

- **Endpoint:** [`stock/quote`](https://historical.flashalpha.com/docs/stock/quote)
- **C# class:** `FlashAlphaStockQuoteBar`
- **Python class:** `StockQuoteBar`
- **Sugar:** `algo.AddFlashAlphaStockQuote("SPY")` / `add_flashalpha_stock_quote(self, "SPY")`

Top-of-book bid / ask / mid / last for the underlier — useful as a sanity check against LEAN's equity book at the same timestamp.

**Wire-format quirk:** the root key is `ticker` (not `symbol`), and the timestamp field is camelCase `lastUpdate` (not `last_update`). Both are aliased on the bar — read `bar.Ticker` and `bar.LastUpdate` as normal.

### Fields

| Field         | Type      | Description                                              | Nullable |
| ------------- | --------- | -------------------------------------------------------- | -------- |
| `Ticker`      | `string`  | JSON `ticker` (not `symbol` on this endpoint).           | Yes      |
| `Bid`         | `double`  | Best bid for the underlier.                              | Yes      |
| `Ask`         | `double`  | Best ask for the underlier.                              | Yes      |
| `Mid`         | `double`  | (Bid + ask) / 2.                                         | Yes      |
| `Last`        | `double`  | Last trade price.                                        | Yes      |
| `LastUpdate`  | `string`  | Last quote/trade update timestamp. JSON key is camelCase `lastUpdate`. | Yes      |

### Sample response

```json
{
  "ticker": "SPY",
  "bid": 478.20,
  "ask": 478.22,
  "mid": 478.21,
  "last": 478.21,
  "lastUpdate": "2024-06-14T19:59:57Z"
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaStockQuoteBar>(_q);
if (bar == null) return;
Debug($"bid={bar.Bid:F2}  ask={bar.Ask:F2}  mid={bar.Mid:F2}");
```

**Python**

```python
bar = slice[self.q]
self.Debug(f"bid={bar.Bid}  ask={bar.Ask}  mid={bar.Mid}")
```

---

## Option quote

- **Endpoint:** [`option/quote`](https://historical.flashalpha.com/docs/option/quote)
- **C# class:** `FlashAlphaOptionQuoteBar`
- **Python class:** `OptionQuoteBar`
- **Sugar:** `algo.AddFlashAlphaOptionQuote("SPY")` / `add_flashalpha_option_quote(self, "SPY")`

The full option chain at the requested minute — one row per contract with bid / ask / mid, IV variants (from-mid, from-bid, from-ask), every first- and second-order Greek, and OI.

**Historical-specific gaps:**

- Per-row `BidSize` / `AskSize` are always 0 (minute table has no sizes).
- Per-row `Volume` is always 0.
- Per-row `SviVol` is always null with `SviVolGated == "backtest_mode"`.

**Reader override:** the upstream JSON root is an array, not an object, so the bar bypasses the standard mapper and deserialises the array directly into `Quotes`.

### Fields

| Field    | Type                    | Description                                                                  | Nullable |
| -------- | ----------------------- | ---------------------------------------------------------------------------- | -------- |
| `Quotes` | `List<OptionQuoteRow>`  | The full option-chain array. Each row's fields are listed below.             | Yes      |

#### `OptionQuoteRow` fields

| Field             | Type      | Description                                                              | Nullable |
| ----------------- | --------- | ------------------------------------------------------------------------ | -------- |
| `Type`            | `string`  | `"C"` or `"P"`.                                                          | Yes      |
| `Expiry`          | `string`  | Expiration date (`yyyy-MM-dd`).                                          | Yes      |
| `Strike`          | `double`  |                                                                          | Yes      |
| `Bid`             | `double`  |                                                                          | Yes      |
| `Ask`             | `double`  |                                                                          | Yes      |
| `Mid`             | `double`  | `(Bid + Ask) / 2`.                                                       | Yes      |
| `BidSize`         | `int`     | Always 0 on historical.                                                  | Yes      |
| `AskSize`         | `int`     | Always 0 on historical.                                                  | Yes      |
| `LastUpdate`      | `string`  | JSON key is camelCase `lastUpdate`.                                      | Yes      |
| `Underlying`      | `double`  | Underlying mid price at the quote time.                                  | Yes      |
| `ImpliedVol`      | `double`  | IV inverted from the mid (annualised %).                                 | Yes      |
| `IvBid`           | `double`  | IV inverted from the bid.                                                | Yes      |
| `IvAsk`           | `double`  | IV inverted from the ask.                                                | Yes      |
| `Delta`           | `double`  |                                                                          | Yes      |
| `Gamma`           | `double`  |                                                                          | Yes      |
| `Theta`           | `double`  |                                                                          | Yes      |
| `Vega`            | `double`  |                                                                          | Yes      |
| `Rho`             | `double`  |                                                                          | Yes      |
| `Vanna`           | `double`  | ∂²V/∂S∂σ.                                                                | Yes      |
| `Charm`           | `double`  | ∂²V/∂S∂t.                                                                | Yes      |
| `SviVol`          | `double`  | Always null on historical (`svi_vol_gated == "backtest_mode"`).          | Yes      |
| `SviVolGated`     | `string`  | Always `"backtest_mode"` on historical.                                  | Yes      |
| `OpenInterest`    | `int`     |                                                                          | Yes      |
| `Volume`          | `int`     | Always 0 on historical.                                                  | Yes      |

### Sample response (truncated)

```json
[
  {"type": "C", "expiry": "2024-06-21", "strike": 480.0, "bid": 1.10, "ask": 1.12, "mid": 1.11,
   "implied_vol": 0.118, "delta": 0.42, "gamma": 0.083, "theta": -0.21, "vega": 0.31,
   "open_interest": 18200, "volume": 0, "svi_vol": null, "svi_vol_gated": "backtest_mode"},
  {"type": "P", "expiry": "2024-06-21", "strike": 480.0, "bid": 2.95, "ask": 2.98, "mid": 2.965,
   "delta": -0.58, "gamma": 0.083, "open_interest": 24310}
]
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaOptionQuoteBar>(_oq);
if (bar?.Quotes == null) return;
var atmCall = bar.Quotes.Find(q => q.Type == "C" && Math.Abs(q.Strike!.Value - 478.0) < 0.01);
if (atmCall != null) Debug($"ATM call mid={atmCall.Mid:F2}  delta={atmCall.Delta:F2}");
```

**Python**

```python
bar = slice[self.oq]
if not bar.Quotes:
    return
atm_call = next(
    (q for q in bar.Quotes if q.get("type") == "C" and abs(q.get("strike", 0) - 478.0) < 0.01),
    None,
)
if atm_call:
    self.Debug(f"ATM call mid={atm_call['mid']}  delta={atm_call['delta']}")
```

---

## Tickers (coverage)

- **Endpoint:** [`tickers`](https://historical.flashalpha.com/docs/tickers)
- **C# class:** `FlashAlphaTickersBar` + `FlashAlphaTickersUniverse`
- **Python class:** `TickersBar` + `FlashAlphaTickersUniverse`
- **Sugar:** `algo.AddFlashAlphaTickers()` / `add_flashalpha_tickers(self)`

The global coverage table — every supported symbol with first/last covered session and a healthy-day count. Use it standalone to introspect coverage, or wire `FlashAlphaTickersUniverse` to drive LEAN universe selection.

**Special case:** this is the only endpoint in the bridge that is *not* ticker-scoped. The bar still subscribes under a LEAN symbol (recommended sentinel: `"_universe"`) but the HTTP layer ignores the ticker when the slug is `"tickers"`.

### Fields

| Field      | Type                | Description                                            | Nullable |
| ---------- | ------------------- | ------------------------------------------------------ | -------- |
| `Tickers`  | `List<TickersRow>`  | Per-symbol coverage rows. Drive a universe selector.   | Yes      |
| `Count`    | `int`               | Length of `Tickers`.                                   | Yes      |

Each `TickersRow` has a `Symbol` and a nested `Coverage` block with `First`, `Last`, and `HealthyDays`.

### Sample response

```json
{
  "count": 3,
  "tickers": [
    {"symbol": "SPY", "coverage": {"first": "2020-01-02", "last": "2024-06-14", "healthy_days": 1112}},
    {"symbol": "QQQ", "coverage": {"first": "2020-01-02", "last": "2024-06-14", "healthy_days": 1108}},
    {"symbol": "IWM", "coverage": {"first": "2020-01-02", "last": "2024-06-14", "healthy_days": 1095}}
  ]
}
```

### Reading the bar

**C#**

```csharp
var bar = slice.Get<FlashAlphaTickersBar>(_tk);
if (bar?.Tickers == null) return;
foreach (var row in bar.Tickers)
    Debug($"  {row.Symbol}  healthy={row.Coverage?.HealthyDays}");
```

**Python**

```python
bar = slice[self.tk]
for row in (bar.Tickers or []):
    cov = row.get("coverage", {})
    self.Debug(f"  {row.get('symbol')}  healthy={cov.get('healthy_days')}")
```

### Driving a universe

```csharp
// C#
public override void Initialize()
{
    AddUniverseSelection(new FlashAlphaTickersUniverse(
        row => (row.Coverage?.HealthyDays ?? 0) > 90));
}
```

```python
# Python
def Initialize(self):
    self.AddUniverseSelection(FlashAlphaTickersUniverse(
        filter=lambda row: row.get("coverage", {}).get("healthy_days", 0) > 90
    ))
```

Full universe walkthrough: [docs/recipes/filter-universe-by-gex-regime.md](recipes/filter-universe-by-gex-regime.md).
