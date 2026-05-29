# Authentication

The bridge talks to `historical.flashalpha.com` using a per-account API key sent as an `X-Api-Key` HTTP header. You set the key once in one of three places; the bridge resolves it for every outgoing request and never logs it.

This doc covers:

- [Where to get a key](#where-to-get-a-key)
- [Setting the key for QC Cloud](#setting-the-key-for-qc-cloud)
- [Setting the key for self-hosted LEAN](#setting-the-key-for-self-hosted-lean)
- [Setting the key for CI](#setting-the-key-for-ci)
- [Key resolution order](#key-resolution-order)
- [Programmatic override](#programmatic-override)
- [Secrets hygiene](#secrets-hygiene)
- [Troubleshooting auth](#troubleshooting-auth)

---

## Where to get a key

Sign up at [flashalpha.com](https://flashalpha.com). Keys are issued from the account dashboard under **API Keys**. The free tier covers the basic exposure endpoints (`exposure/gex`, `exposure/dex`, `exposure/vex`, `exposure/chex`, `exposure/summary`, `exposure/levels`, `surface`, `max-pain`); paid tiers unlock `vrp`, `adv-volatility`, and the composite `stock/summary` endpoint.

Keys begin with `fa_live_` for production keys and `fa_test_` for sandbox keys. Both work against the same API host — the prefix is informational.

---

## Setting the key for QC Cloud

QC Cloud sandboxes the algorithm environment, so env vars from your local shell are not available inside the backtest. Use a project parameter instead.

1. Open your project on [quantconnect.com](https://www.quantconnect.com).
2. Click **Parameters** in the right sidebar (gear icon if collapsed).
3. Click **Add Parameter** and create:
   - Key: `flashalpha-api-key`
   - Value: `fa_live_…` (your key)
4. Save the project.

That's it. The bridge calls `algorithm.GetParameter("flashalpha-api-key")` on the first FlashAlpha request and caches the value for the remainder of the backtest. The parameter does not appear in the algorithm's log output.

If you maintain multiple QC Cloud projects, set the parameter once per project — there's no organization-level shared secret on QC Cloud as of writing.

---

## Setting the key for self-hosted LEAN

For local LEAN runs (`lean backtest`, `lean live`) or any non-cloud LEAN environment, set the `FLASHALPHA_API_KEY` env var before launching LEAN. The bridge picks it up via `Environment.GetEnvironmentVariable` (C#) / `os.environ` (Python).

### Linux / macOS

```bash
export FLASHALPHA_API_KEY="fa_live_..."
lean backtest "MyAlgorithm"
```

To persist for every shell, append the `export` line to `~/.bashrc`, `~/.zshrc`, or whichever shell rc file you use.

### Windows (PowerShell)

```powershell
$env:FLASHALPHA_API_KEY = "fa_live_..."
lean backtest "MyAlgorithm"
```

For a persistent setting:

```powershell
[Environment]::SetEnvironmentVariable("FLASHALPHA_API_KEY", "fa_live_...", "User")
```

You'll need to open a fresh PowerShell window before the value is visible.

### `.env` file with `lean-cli`

The LEAN CLI doesn't auto-load `.env` files, but you can wrap your run command:

```bash
# .env
FLASHALPHA_API_KEY=fa_live_...
```

```bash
set -a; source .env; set +a
lean backtest "MyAlgorithm"
```

The `set -a` / `set +a` pair exports any variable defined between them — necessary because `source` alone leaves them shell-local.

### Docker

If you're running LEAN inside a custom Docker image, pass the env var at run time, never bake it into the image:

```bash
docker run -e FLASHALPHA_API_KEY="fa_live_..." -v $(pwd):/workspace my-lean-image lean backtest
```

---

## Setting the key for CI

The same env-var path applies to CI. Store the key as a CI secret and inject it at job time.

### GitHub Actions

1. Repo → **Settings → Secrets and variables → Actions → New repository secret**.
2. Name: `FLASHALPHA_API_KEY`. Value: `fa_live_…`.
3. In your workflow:

```yaml
jobs:
  backtest:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Install LEAN CLI
        run: pip install lean
      - name: Run backtest
        env:
          FLASHALPHA_API_KEY: ${{ secrets.FLASHALPHA_API_KEY }}
        run: lean backtest "MyAlgorithm"
```

### GitLab CI

```yaml
backtest:
  script:
    - pip install lean
    - lean backtest "MyAlgorithm"
  variables:
    FLASHALPHA_API_KEY: $FLASHALPHA_API_KEY  # masked CI variable
```

Mark the variable as **Protected** and **Masked** under Settings → CI/CD → Variables.

### CircleCI / others

Same shape — store as a project-level env var or secret, inject into the job env. Never echo the value in logs.

---

## Key resolution order

Both languages walk the same resolution chain. The first match wins.

1. **Explicit override.** `FlashAlphaConfig.ApiKey` (C#) / `flashalpha_quantconnect.config.api_key` (Python).
2. **QC Cloud parameter.** `algorithm.GetParameter("flashalpha-api-key")` — only consulted when the bridge has been handed an algorithm reference (it is, on every `AddData` call).
3. **Environment variable.** `FLASHALPHA_API_KEY`.
4. **Throw.** `FlashAlphaAuthMissingException` (error code `FA-AUTH-001`).

The chain exists so the same algorithm runs identically across QC Cloud (parameter), local LEAN (env var), and tests (explicit override) — no per-environment config branch.

---

## Programmatic override

For test fixtures or one-off scripts that want to force a specific key without touching the env, set the explicit override in `Initialize` before any `AddData<…>` call.

### C#

```csharp
using FlashAlpha.QuantConnect;

public override void Initialize()
{
    FlashAlphaConfig.ApiKey = "fa_live_…";   // wins over GetParameter / env var
    _gex = this.AddFlashAlphaGex("SPY").Symbol;
}
```

### Python

```python
from flashalpha_quantconnect import config, add_flashalpha_gex

class MyAlgorithm(QCAlgorithm):
    def Initialize(self):
        config.api_key = "fa_live_…"   # wins over GetParameter / env var
        self.gex = add_flashalpha_gex(self, "SPY").Symbol
```

`FlashAlphaConfig` / `config` also expose `BaseUrl`, `HttpTimeout`, and `MaxRetries` overrides — same wins-over-defaults pattern.

---

## Secrets hygiene

- **Never commit a key.** Add `.env` to `.gitignore`. Use a pre-commit hook (e.g. `detect-secrets`) if your team is large.
- **Rotate aggressively.** Keys are scoped to the issuing account; rotating costs you nothing. If a key leaks (laptop lost, CI log scraped, screenshare), rotate immediately from the dashboard.
- **Don't echo the key.** The bridge's `FlashAlphaUnauthorizedException` (`FA-AUTH-002`) message logs only the last four characters of the rejected key — never the full value. Mirror that pattern in your own diagnostics.
- **Use separate keys per environment.** A `fa_test_…` key for CI plus a `fa_live_…` key for prod is the minimum. Larger teams should issue one key per developer for local LEAN runs so you can revoke per-person.
- **QC Cloud parameter values are not encrypted at rest** in the project view — anyone with edit access on the project can read the parameter. Keep collaborator lists scoped accordingly.

---

## Troubleshooting auth

| Symptom                                                                                              | Likely cause                                       | Fix                                                                                       |
| ---------------------------------------------------------------------------------------------------- | -------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `FlashAlphaAuthMissingException` / `FA-AUTH-001`                                                     | Key not set in any of the three resolution slots   | Set the QC parameter / env var / explicit override. See [docs/troubleshooting.md#fa-auth-001](troubleshooting.md#fa-auth-001). |
| `FlashAlphaUnauthorizedException` / `FA-AUTH-002`                                                    | Key is set but the API rejected it (revoked, typo) | Confirm the key in the FlashAlpha dashboard; check the env var actually exported (`echo $FLASHALPHA_API_KEY`); rotate if leaked. See [docs/troubleshooting.md#fa-auth-002](troubleshooting.md#fa-auth-002). |
| `FlashAlphaUnauthorizedException` only on certain endpoints (e.g. `vrp`, `adv-volatility`)           | Plan tier doesn't include those endpoints          | Upgrade in the FlashAlpha dashboard, or stick to the basic endpoint set.                  |
| Backtest hangs on first FlashAlpha bar                                                               | Wrong base URL or proxy intercept                  | Confirm `FlashAlphaConfig.BaseUrl` is the default `https://historical.flashalpha.com`; check egress firewall rules. |
| Backtest runs locally but fails on QC Cloud                                                          | Parameter not set on the project                   | Add `flashalpha-api-key` under **Project → Parameters** (case-sensitive).                 |

For the full error-code reference and deeper diagnostics see [docs/troubleshooting.md](troubleshooting.md).
