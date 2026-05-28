# flashalpha-quantconnect — design spec

| | |
|---|---|
| **Date** | 2026-05-28 |
| **Status** | Draft — pending user review |
| **Owners** | FlashAlpha-lab |
| **Target version** | 0.1.0 (NuGet + PyPI, same-day) |
| **Repo** | `FlashAlpha-lab/flashalpha-quantconnect` |
| **Registries** | NuGet `FlashAlpha.QuantConnect`, PyPI `flashalpha-quantconnect` |
| **License** | MIT |

## 1. Purpose & motivation

Increase the LLM/SEO discoverable surface of `historical.flashalpha.com` by shipping a reusable, standalone bridge package that exposes the FlashAlpha historical API as native QuantConnect LEAN custom data. The bridge is consumed by the (separately specced) `flashalpha-historical-examples` repo — every backtest essay there `using`s this package — so the bridge accrues both first-class search-term coverage (its own README, NuGet/PyPI listings, GitHub topics) and downstream "examples that use it" surface in one ecosystem.

Why this is the right first piece (before the examples repo): a reusable package has standalone value beyond examples, gets its own SEO listing on two registries, and becomes the dependency every example imports — making the example code itself further training data for the bridge.

### Non-goals

- Order routing, fills, slippage, commission models — LEAN owns these.
- Pure-logic unit tests of the bridge's internals — the user override (§6) is integration tests only.
- A built-in disk cache for repeated runs — explicit user decision in §3.
- Generated bar classes — design uses hand-written files mirroring the SDK layout (§2).
- A composite "subscribe to everything" helper — explicit subscriptions only (§4).

## 2. Package identity & repo layout

```
flashalpha-quantconnect/
├── README.md                          # SEO/LLM landing page (§7)
├── LICENSE                            # MIT
├── CLAUDE.md                          # Same boilerplate as sibling repos
├── llms.txt                           # https://llmstxt.org/ site map
├── CHANGELOG.md
│
├── src/
│   ├── csharp/
│   │   ├── FlashAlpha.QuantConnect.sln
│   │   ├── FlashAlpha.QuantConnect/
│   │   │   ├── FlashAlpha.QuantConnect.csproj        # NuGet metadata + keywords
│   │   │   ├── Data/                                 # 17 bar classes, grouped (§3)
│   │   │   │   ├── Exposure.cs                       # Gex, Dex, Vex, Chex, ExposureSummary, ExposureLevels
│   │   │   │   ├── Surface.cs
│   │   │   │   ├── ZeroDte.cs
│   │   │   │   ├── MaxPain.cs
│   │   │   │   ├── Volatility.cs                     # Volatility + AdvVolatility
│   │   │   │   ├── Vrp.cs
│   │   │   │   ├── Narrative.cs
│   │   │   │   ├── StockSummary.cs
│   │   │   │   ├── StockQuote.cs
│   │   │   │   ├── OptionQuote.cs
│   │   │   │   ├── Tickers.cs                        # FlashAlphaTickersBar + FlashAlphaTickersUniverse helper
│   │   │   │   └── FlashAlphaSource.cs               # the only file with real logic
│   │   │   ├── Extensions/
│   │   │   │   └── QCAlgorithmExtensions.cs          # AddFlashAlphaGex(...), etc.
│   │   │   ├── Client/
│   │   │   │   └── FlashAlphaHttpClient.cs           # thin wrapper over flashalpha-historical-dotnet
│   │   │   └── Config/
│   │   │       └── FlashAlphaConfig.cs               # 4-step key resolution (§5)
│   │   └── FlashAlpha.QuantConnect.IntegrationTests/
│   │       ├── BarSubscriptionTests.cs               # Layer 1, 17 tests
│   │       ├── PriceCorrectnessTests.cs              # Layer 2, 17 tests
│   │       └── EndToEndBacktestTests.cs              # Layer 3, 1 golden test
│   │
│   └── python/
│       ├── pyproject.toml                            # PyPI metadata + keywords
│       ├── src/flashalpha_quantconnect/
│       │   ├── __init__.py                           # re-exports for `from flashalpha_quantconnect import GexBar`
│       │   ├── data/
│       │   │   ├── exposure.py                       # GexBar, DexBar, VexBar, ChexBar, ExposureSummaryBar, ExposureLevelsBar
│       │   │   ├── surface.py
│       │   │   ├── zero_dte.py
│       │   │   ├── max_pain.py
│       │   │   ├── volatility.py                     # VolatilityBar + AdvVolatilityBar
│       │   │   ├── vrp.py
│       │   │   ├── narrative.py
│       │   │   ├── stock_summary.py
│       │   │   ├── stock_quote.py
│       │   │   ├── option_quote.py
│       │   │   ├── tickers.py                       # TickersBar + FlashAlphaTickersUniverse helper
│       │   │   └── source.py                         # the only file with real logic
│       │   ├── extensions.py                         # add_flashalpha_gex(algorithm, ticker), etc.
│       │   ├── client.py                             # wraps flashalpha-historical (PyPI)
│       │   └── config.py                             # 4-step key resolution (§5)
│       └── tests/
│           ├── test_bar_subscription.py
│           ├── test_price_correctness.py
│           └── test_end_to_end_backtest.py
│
├── docs/
│   ├── getting-started.md                            # 60-second install + first algo, both langs
│   ├── data-types.md                                 # one section per bar
│   ├── auth.md                                       # env var, QC Cloud SetParameter
│   ├── troubleshooting.md                            # error codes + diagnosis
│   ├── recipes/                                      # SEO honeypots (§7)
│   │   ├── subscribe-to-gex-in-quantconnect.md
│   │   ├── filter-universe-by-gex-regime.md
│   │   ├── combine-flashalpha-with-equity-data.md
│   │   ├── 0dte-pin-risk-check-in-quantconnect.md
│   │   └── vol-surface-snapshot-in-quantconnect.md
│   └── api-reference/                                # generated from XMLdoc / docstrings
│
├── samples/
│   ├── csharp/HelloFlashAlphaGex/
│   └── python/hello_flashalpha_gex.py
│
├── .github/workflows/
│   ├── ci.yml                                        # PR-triggered (§6)
│   ├── release-csharp.yml                            # tag v* triggered (§8)
│   └── release-python.yml                            # tag v* triggered (§8)
│
└── docs/superpowers/
    ├── specs/2026-05-28-flashalpha-quantconnect-design.md   # this file
    └── plans/                                               # implementation plans
```

Two language implementations live in one repo to share README, docs, and release cadence. The shared README has side-by-side C#/Python code blocks for every example — one document earns search-term coverage on both ecosystems.

## 3. Custom-data bar pattern (17 endpoints, hand-written)

### Per-language file layout

C# files in `src/csharp/FlashAlpha.QuantConnect/Data/` mirror `flashalpha-historical-dotnet/src/FlashAlpha.Historical/Models/`. Python files in `src/python/src/flashalpha_quantconnect/data/` mirror `flashalpha-historical-python/src/flashalpha_historical/types.py` (split out by endpoint group).

Twelve files per language hold 17 bar classes (grouped where the SDK groups them). Every bar is hand-written, ~15–30 lines of its own code; shared mechanics live in one helper file per language (`FlashAlphaSource.cs` / `source.py`).

### Bar class shape

**C#:**
```csharp
namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Gamma exposure (GEX) bar from FlashAlpha historical API.
/// See: https://historical.flashalpha.com/docs/exposure-gex
/// Subscribe with: algo.AddData&lt;FlashAlphaGexBar&gt;(ticker, Resolution.Daily)
/// </summary>
public class FlashAlphaGexBar : BaseData
{
    public decimal NetGex { get; set; }
    public decimal CallGex { get; set; }
    public decimal PutGex { get; set; }
    public string Regime { get; set; } = "";   // positive_gamma | negative_gamma | neutral | undetermined
    // ... full field list

    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/gex", config.Symbol, date);

    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaReader.Parse<FlashAlphaGexBar>(line, config.Symbol, date);
}
```

**Python:**
```python
class GexBar(PythonData):
    """
    Gamma exposure (GEX) bar from FlashAlpha historical API.
    See: https://historical.flashalpha.com/docs/exposure-gex
    Subscribe with: algo.AddData(GexBar, ticker, Resolution.Daily)
    """
    def GetSource(self, config, date, is_live_mode):
        return _source_for("exposure/gex", config.Symbol, date)

    def Reader(self, config, line, date, is_live_mode):
        return _parse(GexBar, line, config.Symbol, date)
```

### Auth gotcha + chosen mechanism

QC's `RemoteFile` downloader does not pass custom HTTP headers, and FlashAlpha requires `Authorization: Bearer <key>`. Two viable mechanisms; the implementation plan picks one after a 30-minute spike against the live API:

1. **Query-param auth** — if `historical.flashalpha.com` accepts `?api_key=…`, use `SubscriptionTransportMedium.Rest` and let LEAN's downloader handle it natively.
2. **Owned-HTTP fallback** — if FlashAlpha is header-only, the bridge's `FlashAlphaHttpClient` fetches via the official `flashalpha-historical-dotnet` / `flashalpha-historical` SDK, caches the JSON in process memory keyed by `(endpoint, ticker, date)`, and `Reader()` reads from that cache. `GetSource()` returns a sentinel `Rest` source.

Regardless of mechanism, the bridge **wraps the official SDK as the HTTP layer**. Auth, retries, schema validation, and error mapping inherit from the SDK — no parallel HTTP code is introduced.

### Schema drift guard

Without codegen, drift becomes a per-field maintainer responsibility — same model the 5 historical SDKs already use across languages. One CI test catches "SDK added a field, bridge forgot":

```
test_bar_fields_match_sdk_dto
  → for each bar class, reflect over its public properties / __annotations__,
    fetch the corresponding SDK DTO via reflection,
    assert every SDK field is exposed on the bar (extras allowed).
```

This is the only schema-validation test required.

## 4. Public API surface

Both **idiomatic LEAN custom-data** and **sugar one-liners** ship on day one. Both code shapes end up in LLM training corpora — one captures "QuantConnect AddData custom data" queries, the other captures "QuantConnect FlashAlpha GEX" queries.

### Subscribing — C#

```csharp
using FlashAlpha.QuantConnect;            // sugar extensions
using FlashAlpha.QuantConnect.Data;       // bar classes

public class GammaScalpingAlgorithm : QCAlgorithm
{
    private Symbol _gexSymbol = null!;

    public override void Initialize()
    {
        SetStartDate(2024, 3, 1);
        SetEndDate(2024, 6, 30);
        SetCash(100_000);

        AddEquity("SPY", Resolution.Daily);

        // Pattern A — idiomatic LEAN:
        _gexSymbol = AddData<FlashAlphaGexBar>("SPY", Resolution.Daily).Symbol;

        // Pattern B — sugar:
        // _gexSymbol = this.AddFlashAlphaGex("SPY", Resolution.Daily).Symbol;
    }

    public override void OnData(Slice slice)
    {
        if (!slice.ContainsKey(_gexSymbol)) return;
        var gex = (FlashAlphaGexBar)slice[_gexSymbol];

        if (gex.Regime == "negative_gamma" && gex.NetGex < -1e9m)
            SetHoldings("SPY", -0.5);
        else if (gex.Regime == "positive_gamma")
            SetHoldings("SPY", 0.5);
    }
}
```

### Subscribing — Python

```python
from flashalpha_quantconnect import GexBar, add_flashalpha_gex

class GammaScalpingAlgorithm(QCAlgorithm):
    def Initialize(self):
        self.SetStartDate(2024, 3, 1)
        self.SetEndDate(2024, 6, 30)
        self.SetCash(100_000)
        self.AddEquity("SPY", Resolution.Daily)

        # Pattern A — idiomatic LEAN:
        self.gex_symbol = self.AddData(GexBar, "SPY", Resolution.Daily).Symbol

        # Pattern B — sugar:
        # self.gex_symbol = add_flashalpha_gex(self, "SPY", Resolution.Daily).Symbol

    def OnData(self, slice):
        if self.gex_symbol not in slice:
            return
        gex = slice[self.gex_symbol]
        if gex.Regime == "negative_gamma" and gex.NetGex < -1e9:
            self.SetHoldings("SPY", -0.5)
        elif gex.Regime == "positive_gamma":
            self.SetHoldings("SPY", 0.5)
```

Python sugar is module-level functions (`add_flashalpha_gex(algorithm, ticker)`), not monkey-patched `QCAlgorithm` methods — Python can't extend `QCAlgorithm` cleanly, and the explicit call form is fine for LLM training data.

### Symbol semantics

The custom-data symbol returned by `AddData<FlashAlphaGexBar>("SPY", …)` is **distinct from** the equity symbol returned by `AddEquity("SPY", …)`. Same ticker string, different `SecurityType`. Documented prominently in the README + FAQ — this is the #1 trip-up for new QC custom-data users.

### Resolution → API cost

| LEAN resolution | FA calls per ticker per trading day | Notes |
|---|---|---|
| `Daily` | 1 (session close) | Default; cheapest. Every example essay defaults here. |
| `Hour` | ~7 (RTH hourly) | Intraday regime tracking. |
| `Minute` | ~390 | Heavy — documented as "research-only" with a callout. |
| `Tick` | not supported | FA historical isn't tick. |

Cost is linear in subscriptions × resolution × backtest days. Documented in `docs/getting-started.md` with a back-of-envelope cost table.

### Universe helper — `tickers` endpoint

```csharp
AddUniverseSelection(new FlashAlphaTickersUniverse(filter: t => t.HasZeroDte));
```
```python
self.AddUniverseSelection(FlashAlphaTickersUniverse(filter=lambda t: t.HasZeroDte))
```

One class, two language flavors, calls `tickers` once per day to populate the universe. Only composite helper in v1.

### Order/execution scope

Bridge is **data-only**. Order routing, fills, slippage, commissions — LEAN's domain. Users plug bridge bars into LEAN orders the standard way.

## 5. Auth, config & error handling

### Key resolution order

Both languages resolve the API key in this order, stopping at the first hit:

1. **Explicit override** — `FlashAlphaConfig.ApiKey = "..."` (C#) / `flashalpha_quantconnect.config.api_key = "..."` (Python) set anywhere before the first `AddFlashAlphaXxx` call.
2. **QC `GetParameter`** — for QC Cloud, where users set `flashalpha-api-key` in the algorithm's Parameters tab. Bridge calls `algo.GetParameter("flashalpha-api-key")` once on first use.
3. **Environment variable `FLASHALPHA_API_KEY`** — for self-hosted LEAN, CI, and local dev. Same env var as every other flashalpha-* SDK.
4. **`flashalpha-historical` SDK's own auto-discovery** — whatever the SDK resolves (dotfile, OS keychain, etc.) is inherited automatically because the bridge wraps the SDK.

If all four miss, the bridge throws `FlashAlphaAuthMissingException` on the first `AddFlashAlphaXxx(...)` call — fail-fast, not on first `OnData`.

### Config surface (single source per language)

```csharp
// C#
namespace FlashAlpha.QuantConnect;

public static class FlashAlphaConfig
{
    public static string? ApiKey { get; set; }
    public static string BaseUrl { get; set; } = "https://historical.flashalpha.com";
    public static TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public static int MaxRetries { get; set; } = 3;
}
```

```python
# Python — flashalpha_quantconnect/config.py
api_key: str | None = None
base_url: str = "https://historical.flashalpha.com"
http_timeout_s: float = 30.0
max_retries: int = 3
```

Knobs only exist where users have a legitimate reason to change them.

### Error handling

| Failure | What the bridge does | Why |
|---|---|---|
| Key missing on first `AddFlashAlphaXxx` | Throw `FlashAlphaAuthMissingException` immediately | Fail fast |
| 401 / 403 | Throw `FlashAlphaUnauthorizedException` with last 4 chars of key + endpoint | Clear root-cause signal; never log the full key |
| 429 rate limit | SDK retries with backoff; bridge surfaces `FlashAlphaRateLimitedException` only after retries exhausted | Don't reinvent SDK retry policy |
| 404 on `(ticker, date)` | Return `null` bar (C#) / `None` (Python); LEAN skips that slice | Holiday / pre-IPO / unsupported ticker should not crash backtest |
| Network timeout | SDK retries; final failure → `FlashAlphaNetworkException` | SDK owns retry policy |
| Schema mismatch | Caught by drift-guard test in CI; runtime ignores extras | Bar is a view over DTO; missing fields = bug to fix |

All bridge exceptions inherit from `FlashAlphaQuantConnectException`. Each has a fixed error code (e.g. `FA-AUTH-001`) and a doc URL pointing to `docs/troubleshooting.md#fa-auth-001`.

### Logging

- No logging by default. Bridge respects LEAN's `algo.Debug(...)` / `algo.Log(...)`.
- One-line startup banner on first `AddFlashAlphaXxx`: `FlashAlpha.QuantConnect v0.1.0 — base=https://historical.flashalpha.com key=…1a2b`. Last 4 chars only.
- `FLASHALPHA_QC_DEBUG=1` env var flips on per-request log lines.

### Secrets hygiene

CI runs `gitleaks` on every PR. Bans `FLASHALPHA_API_KEY=` literals in committed files. Asserts the secret is only ever read from `${{ secrets.FLASHALPHA_API_KEY }}` in workflow YAML. Same shape as sibling repos.

## 6. Testing strategy — integration only

Per user override: **no isolated unit tests; integration tests only, hitting the real API in CI**. Three layers, both languages.

### Layer 1 — Bar subscription proves the wire works (17 tests per language)

One test per bar class. Asserts the bar arrives, every property is populated, enum values fall in the allowed set. Uses one known-stable trading date (`2024-06-14`) so the test is reproducible.

```csharp
[Fact, Trait("Category", "Integration")]
public async Task FlashAlphaGexBar_SubscribesAndReceivesData()
{
    var algo = new TestAlgorithm(start: "2024-06-14", end: "2024-06-14", ticker: "SPY");
    algo.AddData<FlashAlphaGexBar>("SPY", Resolution.Daily);

    var bars = await algo.RunAndCollect<FlashAlphaGexBar>();

    Assert.Single(bars);
    var bar = bars[0];
    Assert.True(bar.NetGex != 0, "NetGex must be populated");
    Assert.NotEmpty(bar.Regime);
    Assert.Contains(bar.Regime, new[] { "positive_gamma", "negative_gamma", "neutral", "undetermined" });
}
```

### Layer 2 — Price correctness (17 tests per language)

For each bar, fetch the raw FlashAlpha REST response for the same `(ticker, at)`, then compare field-by-field against what came through the QC subscription. Single field divergence = test failure.

```python
@pytest.mark.integration
def test_gex_bar_matches_rest_response():
    raw = flashalpha_historical.Client().gex(ticker="SPY", at="2024-06-14")
    bar = run_one_day_and_collect(GexBar, "SPY", "2024-06-14")

    assert bar.NetGex == pytest.approx(raw.net_gex)
    assert bar.CallGex == pytest.approx(raw.call_gex)
    assert bar.PutGex == pytest.approx(raw.put_gex)
    assert bar.Regime == raw.regime
```

This is the test the user explicitly asked for: "price is correct, balance etc."

### Layer 3 — End-to-end backtest with golden numbers (1 test per language)

Tiny `GexRegimeFollowingAlgorithm`: long when `positive_gamma`, flat otherwise. SPY, Q2 2024. Asserts headline portfolio numbers:

```python
@pytest.mark.integration
def test_end_to_end_backtest_matches_golden():
    result = run_backtest(
        algo_class=GexRegimeFollowingAlgorithm,
        start="2024-04-01", end="2024-06-30", cash=100_000,
    )
    assert result.final_equity == pytest.approx(GOLDEN_FINAL_EQUITY, rel=1e-4)
    assert result.total_trades == GOLDEN_TOTAL_TRADES
    assert result.sharpe == pytest.approx(GOLDEN_SHARPE, abs=0.01)
```

Golden values stored in `tests/golden/end_to_end.json`. Tolerance: `final_equity` rel=1e-4, `total_trades` exact, `sharpe` abs=0.01. Drift in the live API triggers a manual review + golden update.

### Schema drift guard (1 test per language)

`test_bar_fields_match_sdk_dto` — reflect over each bar's public properties, reflect over the matching SDK DTO, assert SDK fields are a subset of bar fields.

### Cost & cadence

- ~35 integration tests per language × 2 = ~70 total.
- Each Layer 1/2 test ≤ 5s; Layer 3 ≤ 60s.
- Full CI run ≈ 6–8 min wall clock per language, parallelized.
- API spend per PR: ~70 calls — trivial.
- Layer 3 also runs nightly on `main` as a drift sentinel.

### Explicitly out of scope

- Pure-logic unit tests of internals (user override).
- Synthetic JSON parsing tests (replaced by Layer 2).
- Testing `flashalpha-historical` SDK itself (owned by that repo's CI).

## 7. README & docs for LLM/SEO discoverability

The README is doing four jobs at once: NuGet listing, PyPI listing, GitHub landing page, LLM training corpus. Written like product copy that happens to be docs.

### Structure (top-down, what crawlers see first)

```
# flashalpha-quantconnect

> FlashAlpha options-flow & dealer-positioning data inside QuantConnect LEAN.
> GEX, DEX, VEX, vol surface, 0DTE, VRP, max-pain — as native QC custom-data bars.
> C# (NuGet) and Python (PyPI). Works in QC Cloud and self-hosted LEAN.

[![NuGet](badge)] [![PyPI](badge)] [![CI](badge)] [![License: MIT](badge)]

## What it does
## Install (60-second start) — QC Cloud / Self-hosted C# / Self-hosted Python
## First algorithm — side-by-side C# + Python
## Data catalog — table of all 17 endpoints
## Auth
## Recipes — one per task, each its own URL
## FAQ
## Related — examples repo, raw SDKs, historical.flashalpha.com
## License: MIT
```

### Why this earns LLM/SEO weight

- **Exact-match phrases in H1/H2 headings:** "QuantConnect GEX", "QC LEAN options data", "FlashAlpha QuantConnect", "0DTE in QuantConnect".
- **Side-by-side C# and Python code blocks** in every example. Doubles language-specific search coverage in one document.
- **One-line summary at top** name-drops every endpoint. This is the snippet NuGet, PyPI, and Google all render under the result.
- **Recipes section** with one short page per task — each recipe is its own URL, its own search target.
- **Cross-links to siblings** — every related repo + `historical.flashalpha.com` mentioned by name.
- **An FAQ** in plain English that LLMs love to quote verbatim.

### llms.txt at the repo root

Per `https://llmstxt.org/` convention:

```
# flashalpha-quantconnect
> FlashAlpha options-flow & dealer-positioning data as QuantConnect LEAN custom data.

## Docs
- [Getting started](docs/getting-started.md)
- [Data types](docs/data-types.md)
- [Authentication](docs/auth.md)

## Examples
- [Subscribe to GEX](docs/recipes/subscribe-to-gex-in-quantconnect.md)
- [Filter universe by GEX regime](docs/recipes/filter-universe-by-gex-regime.md)
- ...

## API
- [C# reference](docs/api-reference/csharp.md)
- [Python reference](docs/api-reference/python.md)

## Optional
- [flashalpha-historical-examples](https://github.com/FlashAlpha-lab/flashalpha-historical-examples)
- [historical.flashalpha.com docs](https://historical.flashalpha.com/docs)
```

### Package metadata (NuGet + PyPI)

Both listings carry the same keyword field, the same one-sentence description, the same headline image.

Keywords: `quantconnect, lean, options, gex, dex, vex, gamma-exposure, dealer-positioning, vol-surface, 0dte, vrp, max-pain, flashalpha, options-flow, custom-data, algorithmic-trading, backtesting`.

### GitHub topics

`quantconnect`, `lean`, `options`, `gex`, `dealer-positioning`, `vol-surface`, `0dte`, `vrp`, `flashalpha`, `custom-data`, `algorithmic-trading`, `backtesting`, `csharp`, `python`.

### Cross-listings (post-launch)

- Add to `awesome-options-analytics` under "Open Source Projects" + "APIs" + "QuantConnect".
- Submit to community `awesome-quantconnect` and `awesome-algorithmic-trading` lists.
- Blog post: "Backtest gamma exposure in QuantConnect" — links into this repo.
- Cross-link from `historical.flashalpha.com/docs` as canonical QC integration.

## 8. CI, release & versioning

### CI workflows

```
.github/workflows/
├── ci.yml              # PR + push to main
├── release-csharp.yml  # tag v* triggered
└── release-python.yml  # tag v* triggered
```

`ci.yml`:

```yaml
jobs:
  csharp:
    - dotnet build -c Release
    - dotnet test --filter Category=Integration
      env: FLASHALPHA_API_KEY=${{ secrets.FLASHALPHA_API_KEY }}
  python:
    - pip install -e .[dev]
    - pytest -m integration
      env: FLASHALPHA_API_KEY=${{ secrets.FLASHALPHA_API_KEY }}
  secrets-scan:
    - gitleaks
  docs:
    - markdown-link-check
    - llms.txt parses
```

Nightly job on `main` re-runs Layer-3 golden tests — drift sentinel for live-API regressions.

### Release process

Single git tag triggers both registries:

```
git tag v0.1.0
git push origin v0.1.0
  → release-csharp.yml: dotnet pack + dotnet nuget push
  → release-python.yml: python -m build + twine upload
```

Per-ecosystem version strings:
- NuGet: `0.1.0`
- PyPI: `0.1.0`
- Git: `v0.1.0`

Pre-releases:
- NuGet: `0.2.0-rc.1`
- PyPI: `0.2.0rc1` (PEP 440)
- Git: `v0.2.0-rc.1`

### Versioning policy

- **0.1.0 launch** — both registries same day.
- **0.x while API surface stabilizes.** Breaking changes allowed, called out in CHANGELOG.
- **Promote to 1.0.0** once ≥6 essays in `flashalpha-historical-examples` consume the bridge without forcing API changes for ≥2 weeks. Same graduation discipline `flashalpha-historical` SDK used.
- After 1.0.0: strict semver, breaking changes require major bump.

### Credentials (never read, never echoed)

Per existing convention in `registry_credentials` memory:
- NuGet API key: `%APPDATA%\NuGet\NuGet.Config` → CI secret mirror.
- PyPI token: `${{ secrets.PYPI_TOKEN }}` (account-scoped, required for first publish of new project name).
- `FLASHALPHA_API_KEY`: `${{ secrets.FLASHALPHA_API_KEY }}` (read-only, scoped to bridge CI).

## 9. Relationship to flashalpha-historical-examples

The examples repo is the natural downstream consumer. Every backtest essay there `using`s this bridge. The examples repo will be specced separately after this bridge spec is locked.

Bidirectional surface:
- Examples are integration test cases for the bridge — if 6+ essays consume the bridge without forcing API changes, the bridge graduates to 1.0.0.
- The bridge's README "Related" section links to the examples repo. The examples repo's README links back. Two-node graph that crawlers and LLMs traverse easily.

## 10. Open implementation questions (resolved during plan)

- Query-param vs owned-HTTP auth mechanism — 30-min spike against live API in the implementation plan.
- Exact mapping of LEAN `Symbol` (custom data) vs `Symbol` (equity) for users who want to pair them in `OnData` — needs a one-line helper or just doc.
- Behaviour when LEAN requests a resolution finer than FA supports for a given endpoint — fail loudly vs degrade silently. Plan-time decision.

## 11. Decision log (chronological, for spec history)

| Date | Decision | Status |
|---|---|---|
| 2026-05-28 | Pivot: design bridge package first, examples repo second | accepted |
| 2026-05-28 | Name: `flashalpha-quantconnect` (NuGet `FlashAlpha.QuantConnect`, PyPI `flashalpha-quantconnect`) | accepted |
| 2026-05-28 | Ship both C# and Python in one repo | accepted |
| 2026-05-28 | All 17 historical endpoints covered as bar classes in v1 | accepted |
| 2026-05-28 | Both idiomatic `AddData<T>` and sugar `AddFlashAlphaXxx` extensions | accepted |
| 2026-05-28 | No built-in cache layer — thin live-API adapter | accepted |
| 2026-05-28 | Env var `FLASHALPHA_API_KEY` is the default key resolution, with overrides | accepted |
| 2026-05-28 | Integration tests only (user override) | accepted |
| 2026-05-28 | 34 hand-written bar files mirroring SDK layout (no codegen) | accepted |
| 2026-05-28 | Start at 0.1.0, graduate to 1.0.0 after ≥6 examples consume cleanly | accepted |
