# Troubleshooting

Every exception the bridge throws carries a stable `ErrorCode` (`FA-AUTH-001`, `FA-AUTH-002`, `FA-RATE-001`, `FA-NET-001`). Each code maps onto an anchor on this page; clicking through from a stack trace lands you on the right section. The anchors are lowercased — e.g. `FA-AUTH-001` → [`#fa-auth-001`](#fa-auth-001) — and the exception's `DocUrl` property points at this file with the right fragment already attached.

This doc covers:

- [FA-AUTH-001 — Missing API key](#fa-auth-001)
- [FA-AUTH-002 — Unauthorized](#fa-auth-002)
- [FA-RATE-001 — Rate limited](#fa-rate-001)
- [FA-NET-001 — Network error](#fa-net-001)
- [Why two symbols? (custom-data vs equity)](#why-two-symbols)
- [Bar fields are null](#bar-fields-null)
- [Tier-restricted endpoints](#tier-restricted)
- [Cold-cache slowness on `adv-volatility`](#cold-cache-adv-vol)

---

## fa-auth-001

**Missing API key.** Thrown as `FlashAlphaAuthMissingException` when the key resolution chain — explicit override → QC Cloud `GetParameter` → `FLASHALPHA_API_KEY` env var — finds nothing.

### Symptoms

- Algorithm dies on the first FlashAlpha bar request, before any `OnData` fires.
- Exception message: `FlashAlpha API key not found. Set FLASHALPHA_API_KEY env var, FlashAlphaConfig.ApiKey, or QC parameter 'flashalpha-api-key'.`
- QC Cloud: the cloud log shows the same message under the algorithm's runtime errors.

### Causes

1. The env var is set in your shell but not exported to LEAN's process — `lean backtest` launched from a different shell, IDE run config, or systemd unit doesn't inherit it.
2. On QC Cloud, the parameter is named differently (e.g. `flashalpha_api_key` with underscores) or has trailing whitespace.
3. The explicit override is being set after the first `AddData` call, so the bridge has already failed.

### Fixes

- **Confirm the env var is actually visible to LEAN.** From the same shell you launch LEAN with: `echo "$FLASHALPHA_API_KEY"`. Empty? Export it again.
- **Match the QC parameter name exactly.** It is `flashalpha-api-key` — hyphens, lowercase. No leading or trailing spaces. Recreate it if in doubt.
- **Set the explicit override at the top of `Initialize`.** Setting it in a custom helper called from `OnData` is too late — the first `AddData` line resolves the key.
- **Mind whitespace on copy/paste.** A key with a leading or trailing space won't compare equal to the resolver's `string.IsNullOrEmpty` check on the wire; the API will reject it with `FA-AUTH-002`, not `FA-AUTH-001`. If you see both errors flip-flopping, the value is non-empty but malformed.

---

## fa-auth-002

**Unauthorized.** Thrown as `FlashAlphaUnauthorizedException` when the API returns HTTP 401 / 403 after a request reaches the wire.

### Symptoms

- Algorithm starts, the first FlashAlpha request goes out, then the bridge raises after the SDK call returns.
- Exception message includes the endpoint and the **last four characters of the key**, never the full value: `FlashAlpha rejected the API key (…AbCd) on endpoint exposure/gex. See troubleshooting docs.`
- The dashboard on flashalpha.com shows a recent failed request for the key.

### Causes

1. **Wrong key.** Typo, stale key from a prior environment, picked the wrong one out of a password manager.
2. **Revoked key.** A previous admin rotated it.
3. **Plan tier doesn't cover the endpoint.** `vrp`, `adv-volatility`, and `stock/summary` return 403 for free-tier keys.
4. **Geo / IP restriction.** Some accounts ship with allow-listed IPs; CI runners outside the allow list get 403.

### Fixes

- **Confirm the key in the FlashAlpha dashboard.** Last four characters should match the `(…AbCd)` in the exception. If they don't, you're not picking up the key you think you are — re-check the resolution chain.
- **Check plan tier.** If only certain endpoints fail (e.g. `vrp` 403, `gex` 200) you've outgrown the free tier — upgrade or remove the `AddData<…>` for the restricted bar.
- **Allow-list the runner IP** if you've configured IP restrictions on the key, or remove the restriction for CI runs.
- **Rotate the key** if there's any chance it leaked. The dashboard can issue a fresh one in seconds.

---

## fa-rate-001

**Rate limited (after SDK retries exhausted).** Thrown as `FlashAlphaRateLimitedException` when the API returns HTTP 429 and the SDK's built-in exponential-backoff retry loop fails to recover.

### Symptoms

- Algorithm runs for a while, then dies mid-backtest on a particular bar request.
- Exception message: `FlashAlpha rate-limited the request to <endpoint> after retries exhausted.`
- The bridge surfaces the underlying SDK exception via `InnerException`.

### Causes

1. **Backtest is at minute resolution.** ~390 calls per ticker per day; a 252-day SPY backtest is ~98k calls — easy to brush against tier limits.
2. **Many tickers in the universe.** A 500-name universe with daily GEX is 500 calls per day for that bar alone; mix in DEX / VEX and you triple the cost.
3. **Concurrent backtests on the same key.** Local LEAN backtest, CI run, and a teammate's QC Cloud run on the same key collide on the per-second window.

### Fixes

- **Drop to `Resolution.Daily`** unless you genuinely need intraday snapshots. One daily call has the same headline `net_gex`, `gamma_flip`, and `net_gex_label` you'd read off any minute.
- **Cache locally.** The bridge has a per-process cache so multiple subscriptions on `(endpoint, ticker, date)` reuse the same response. Move shared logic into a single bar where you can. For research, dump the bars to disk and replay them.
- **Throttle the universe.** Gate `FlashAlphaTickersUniverse` on `coverage.healthy_days` so you only pull the well-covered names. See [docs/recipes/filter-universe-by-gex-regime.md](recipes/filter-universe-by-gex-regime.md).
- **Increase `MaxRetries`** as a last resort: `FlashAlphaConfig.MaxRetries = 5` (C#) / `config.max_retries = 5` (Python). This trades latency for resilience.
- **Upgrade your plan** if you've genuinely outgrown the rate.

---

## fa-net-001

**Network error.** Thrown as `FlashAlphaNetworkException` when the SDK can't reach `historical.flashalpha.com` — DNS failure, TLS handshake failure, connection reset.

### Symptoms

- Exception message: `Network error talking to FlashAlpha at <endpoint>.`
- `InnerException` is typically `HttpRequestException` (C#) or `httpx.ConnectError` / `httpx.ReadTimeout` (Python).
- Other internet traffic from the same host succeeds — only `historical.flashalpha.com` fails.

### Causes

1. **Corporate proxy or firewall.** Egress to `*.flashalpha.com:443` is blocked.
2. **DNS misconfiguration.** Local resolver can't see `historical.flashalpha.com`.
3. **TLS / cert validation failure.** Out-of-date system trust store, or a MITM proxy injecting its own cert.
4. **Server-side hiccup.** Rare; FlashAlpha publishes incidents.

### Fixes

- **Curl from the same host.** `curl -I https://historical.flashalpha.com/v1/ping` — should return `200 OK`. If it doesn't, the issue is below the bridge.
- **Configure an HTTPS proxy** if your network requires one — both Python (`HTTPS_PROXY` env var) and .NET (`HttpClient`'s default proxy detection) respect the standard env vars.
- **Bump the timeout.** `FlashAlphaConfig.HttpTimeout = TimeSpan.FromSeconds(60)` (C#) / `config.http_timeout_s = 60.0` (Python) for slow corporate networks.
- **Update root certs** if TLS validation is failing — Ubuntu `apt install --reinstall ca-certificates`, macOS automatic via system update, Windows via Windows Update.
- **Check [status.flashalpha.com](https://status.flashalpha.com)** for an active incident.

---

## why-two-symbols

The most common surprise on this bridge: **a FlashAlpha custom-data Symbol is not the same Symbol you get from `AddEquity`.** They look identical when printed (both say `SPY`), but they are distinct LEAN identities, and `Slice` lookups by one will not return data for the other.

### What's happening

LEAN's custom-data subscription system mints a fresh `Symbol` for each `AddData<TBar>(ticker, …)` call, distinct from the equity `Symbol` that `AddEquity(ticker, …)` returns. The two symbols share a ticker string but are entirely separate subscriptions internally — they show up in different slots of the `Slice` and must be queried independently.

### Diagnostic

If `slice.ContainsKey(myEquitySymbol)` returns true but `slice.Get<FlashAlphaGexBar>(myEquitySymbol)` returns `null`, this is what's biting you. Same the other direction.

### Fix — hold both symbols as fields

```csharp
private Symbol _spy;     // from AddEquity
private Symbol _gex;     // from AddData / AddFlashAlphaGex

public override void Initialize()
{
    _spy = AddEquity("SPY", Resolution.Daily).Symbol;
    _gex = this.AddFlashAlphaGex("SPY").Symbol;
}

public override void OnData(Slice slice)
{
    if (!slice.ContainsKey(_gex)) return;
    var gex = slice.Get<FlashAlphaGexBar>(_gex);
    // SetHoldings on the EQUITY symbol, not the GEX symbol.
    SetHoldings(_spy, gex.NetGexLabel == "positive" ? 1.0m : 0m);
}
```

For multi-ticker setups, pair the two by ticker string with a `Dictionary<string, Symbol>`:

```csharp
private readonly Dictionary<string, Symbol> _equity = new();
private readonly Dictionary<string, Symbol> _gex = new();

public override void Initialize()
{
    foreach (var t in new[] { "SPY", "QQQ", "IWM" })
    {
        _equity[t] = AddEquity(t, Resolution.Daily).Symbol;
        _gex[t]    = this.AddFlashAlphaGex(t).Symbol;
    }
}
```

There's a full multi-ticker walkthrough in [docs/recipes/combine-flashalpha-with-equity-data.md](recipes/combine-flashalpha-with-equity-data.md).

### Why doesn't the bridge unify the symbols?

QC's API doesn't let you. `AddData<T>` returns a `BaseData`-typed subscription whose `Symbol` is owned by the custom-data subsystem; we have no hook to attach it to an existing equity `Symbol`. Every custom-data provider in the LEAN ecosystem has the same constraint.

---

## bar-fields-null

A bar arrives in `OnData` but key fields read as `null` / `None` / `0`.

### Causes by bar

- **GEX / DEX / VEX / CHEX.** `Strikes` per-row `CallVolume` and `PutVolume` are placeholders on historical — the minute table doesn't retain intraday volume. Use the `CallOi` / `PutOi` fields for the historical positioning view.
- **Option-quote.** Per-row `BidSize` / `AskSize` / `Volume` are always 0 on historical; `SviVol` is always `null` with `SviVolGated == "backtest_mode"`. Documented in the bar docstring.
- **Stock summary.** `OptionsFlow.TotalCallVolume`, `TotalPutVolume`, and `PcRatioVolume` are 0 / null — no minute volume on replay. `Macro.VixFutures` and `Macro.FearAndGreed` are always null on historical (CME futures and the CNN index are not historically reconstructible).
- **VRP.** `StrategyScores` and `NetHarvestScore` are null on early historical timestamps with insufficient warmup — check the `Warnings` list. Also: `ZScore` and `Percentile` live on `bar.Vrp`, not the top level; `NetGex` lives on `bar.Regime`. These look like null at the top level but are populated one nesting in.
- **Zero-DTE.** On names with no same-day expiry the entire bar is "thin" — `NoZeroDte = true`, `Message` populated, every other block `null`, `NextZeroDteExpiry` pointing at the next available expiry. Null-check explicitly:

  ```python
  if bar.NoZeroDte:
      self.Debug(f"No 0DTE today — next is {bar.NextZeroDteExpiry}")
      return
  ```

### General null-safety

Always null-check nested blocks before drilling in. The `Slice.Get<T>` indexer can return `null` if the bar didn't arrive on this slice (e.g. weekends, holidays), and the bar itself can have nullable nested blocks even on a successful response.

```csharp
var bar = slice.Get<FlashAlphaZeroDteBar>(_zeroDte);
if (bar?.PinRisk == null) return;
var pinScore = bar.PinRisk.Score;
```

```python
bar = slice[self.zero_dte]
pin_risk = bar.PinRisk or {}
pin_score = pin_risk.get("score")
if pin_score is None:
    return
```

---

## tier-restricted

The API returned a 403 with the body `{"error":"tier_restricted"}`. The bridge surfaces this as `FlashAlphaUnauthorizedException` — same error code path as a bad key.

Endpoints currently gated by plan tier:

| Endpoint           | Required tier |
| ------------------ | ------------- |
| `vrp`              | Alpha or above |
| `adv-volatility`   | Alpha or above |
| `stock/summary`    | Alpha or above |

Fix: upgrade in the FlashAlpha dashboard, or remove the `AddData<…>` for the restricted bar. The other 14 endpoint families are free-tier.

---

## cold-cache-adv-vol

The `adv-volatility` endpoint runs SVI calibration plus full surface arbitrage checks on each request. **Cold-cache responses can take ~1.5s.** First request after a process restart, or on a date that's never been requested before, blows past the default 30-second HTTP timeout only on pathological surfaces — but per-bar latency is noticeably higher than `gex` or `surface`.

If you're hitting timeouts:

- Bump `FlashAlphaConfig.HttpTimeout` to 60 seconds.
- Don't subscribe `adv-volatility` minute-by-minute. The endpoint is daily-cadence in spirit; minute subscriptions multiply cost without giving you fresh SVI parameters between bars.
- Pre-warm the cache by hitting the date range in a research notebook (`lean research`) before running the backtest.
