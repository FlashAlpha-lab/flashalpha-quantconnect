# flashalpha-quantconnect Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `flashalpha-quantconnect` bridge package (C# + Python) that exposes the FlashAlpha historical API as QuantConnect LEAN custom-data bars, ready for v0.1.0 publish to NuGet + PyPI.

**Architecture:** Two parallel language implementations in one repo, sharing README/docs/release cadence. Each language has 17 hand-written `BaseData`/`PythonData` subclasses grouped into 12 files mirroring the historical SDK's `Models/` layout, plus a single `FlashAlphaSource` helper file that owns all real HTTP/auth/parse logic. The bridge wraps the official `flashalpha-historical-{dotnet,python}` SDKs as its HTTP layer rather than re-implementing HTTP. Tests are integration-only against the live API in CI (user override) — three layers per bar: subscription works, fields match REST response, end-to-end backtest hits golden numbers.

**Tech Stack:** C# (.NET 8, xUnit, QuantConnect.Lean nuget), Python (3.10+, pytest, quantconnect/lean Python bindings), `flashalpha-historical-dotnet` (NuGet `FlashAlpha.Historical` ≥ 0.4.0-rc.1), `flashalpha-historical` (PyPI ≥ 0.4.0rc1), GitHub Actions, NuGet, PyPI.

**Spec reference:** [docs/superpowers/specs/2026-05-28-flashalpha-quantconnect-design.md](../specs/2026-05-28-flashalpha-quantconnect-design.md)

---

## Conventions used throughout this plan

- **TDD cycle per task:** failing test → run → minimal impl → run passes → commit. Always in that order.
- **Commits:** one commit per task (not per step). Conventional Commits prefixes (`feat:`, `test:`, `chore:`, `docs:`, `ci:`).
- **Working directory:** all paths relative to `e:/repos/tecware/flashalpha-packages/flashalpha-quantconnect/` unless absolute.
- **Two languages, one repo:** when a task touches both C# and Python, the steps interleave by language. Each language's steps are self-contained — the engineer can complete C# fully before starting Python if they prefer.
- **Live API key required:** every test below `[Trait("Category", "Integration")]` / `@pytest.mark.integration` requires `FLASHALPHA_API_KEY` in the environment. Local: export it or use `.env`. CI: `${{ secrets.FLASHALPHA_API_KEY }}`.
- **Source-of-truth for endpoint schemas:** the `flashalpha-historical-dotnet/src/FlashAlpha.Historical/Models/*.cs` POCOs and `flashalpha-historical-python/src/flashalpha_historical/types.py` TypedDicts. When a bar's field set is ambiguous, open the SDK's matching response type, list its fields, mirror them in the bar.

---

## Phase 0 — Skeleton & auth spike (3 tasks)

### Task 1: C# solution + project skeleton

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect.sln`
- Create: `src/csharp/FlashAlpha.QuantConnect/FlashAlpha.QuantConnect.csproj`
- Create: `src/csharp/FlashAlpha.QuantConnect/AssemblyInfo.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/FlashAlpha.QuantConnect.IntegrationTests.csproj`
- Create: `src/csharp/Directory.Build.props`

- [ ] **Step 1: Verify parent directories exist**

Run: `ls e:/repos/tecware/flashalpha-packages/flashalpha-quantconnect/`
Expected: `LICENSE`, `README.md`, `.gitignore`, `docs/` already present from spec phase.

- [ ] **Step 2: Create the C# project skeleton**

```bash
mkdir -p src/csharp/FlashAlpha.QuantConnect/{Data,Extensions,Client,Config}
mkdir -p src/csharp/FlashAlpha.QuantConnect.IntegrationTests/{Subscription,PriceCorrectness,EndToEnd,Fixtures}
```

- [ ] **Step 3: Write `src/csharp/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>1591</NoWarn>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Write `src/csharp/FlashAlpha.QuantConnect/FlashAlpha.QuantConnect.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>FlashAlpha.QuantConnect</PackageId>
    <Version>0.1.0</Version>
    <Authors>FlashAlpha</Authors>
    <Description>FlashAlpha options-flow and dealer-positioning data as QuantConnect LEAN custom-data bars. GEX, DEX, VEX, vol surface, 0DTE, VRP, max-pain.</Description>
    <PackageTags>quantconnect;lean;options;gex;dex;vex;gamma-exposure;dealer-positioning;vol-surface;0dte;vrp;max-pain;flashalpha;options-flow;custom-data;algorithmic-trading;backtesting</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/FlashAlpha-lab/flashalpha-quantconnect</PackageProjectUrl>
    <RepositoryUrl>https://github.com/FlashAlpha-lab/flashalpha-quantconnect</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="QuantConnect.Lean" Version="2.5.*" />
    <PackageReference Include="FlashAlpha.Historical" Version="0.4.0-rc.1" />
  </ItemGroup>

  <ItemGroup>
    <None Include="../../../README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Write `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/FlashAlpha.QuantConnect.IntegrationTests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FlashAlpha.Historical" Version="0.4.0-rc.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../FlashAlpha.QuantConnect/FlashAlpha.QuantConnect.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Generate the solution file**

```bash
cd src/csharp
dotnet new sln -n FlashAlpha.QuantConnect
dotnet sln add FlashAlpha.QuantConnect/FlashAlpha.QuantConnect.csproj
dotnet sln add FlashAlpha.QuantConnect.IntegrationTests/FlashAlpha.QuantConnect.IntegrationTests.csproj
```

- [ ] **Step 7: Verify the skeleton compiles**

Run: `dotnet build src/csharp/FlashAlpha.QuantConnect.sln`
Expected: `Build succeeded. 0 Error(s).` Both projects compile (empty but valid).

- [ ] **Step 8: Commit**

```bash
git add src/csharp/
git commit -m "chore: scaffold C# project skeleton with QC.Lean + FlashAlpha.Historical refs"
```

---

### Task 2: Python package skeleton

**Files:**
- Create: `src/python/pyproject.toml`
- Create: `src/python/src/flashalpha_quantconnect/__init__.py`
- Create: `src/python/tests/__init__.py`
- Create: `src/python/tests/conftest.py`
- Create: `src/python/README.md` (symlinked from root for PyPI)

- [ ] **Step 1: Create directories**

```bash
mkdir -p src/python/src/flashalpha_quantconnect/data
mkdir -p src/python/tests/{subscription,price_correctness,end_to_end,fixtures}
```

- [ ] **Step 2: Write `src/python/pyproject.toml`**

```toml
[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[project]
name = "flashalpha-quantconnect"
version = "0.1.0"
description = "FlashAlpha options-flow and dealer-positioning data as QuantConnect LEAN custom-data bars. GEX, DEX, VEX, vol surface, 0DTE, VRP, max-pain."
readme = "README.md"
license = "MIT"
authors = [{ name = "FlashAlpha" }]
requires-python = ">=3.10"
keywords = [
  "quantconnect", "lean", "options", "gex", "dex", "vex",
  "gamma-exposure", "dealer-positioning", "vol-surface",
  "0dte", "vrp", "max-pain", "flashalpha", "options-flow",
  "custom-data", "algorithmic-trading", "backtesting",
]
classifiers = [
  "License :: OSI Approved :: MIT License",
  "Programming Language :: Python :: 3",
  "Programming Language :: Python :: 3.10",
  "Programming Language :: Python :: 3.11",
  "Programming Language :: Python :: 3.12",
  "Topic :: Office/Business :: Financial :: Investment",
]
dependencies = [
  "flashalpha-historical>=0.4.0rc1",
]

[project.optional-dependencies]
dev = [
  "pytest>=7",
  "pytest-asyncio",
]

[project.urls]
Homepage = "https://github.com/FlashAlpha-lab/flashalpha-quantconnect"
Repository = "https://github.com/FlashAlpha-lab/flashalpha-quantconnect"
Documentation = "https://github.com/FlashAlpha-lab/flashalpha-quantconnect#readme"

[tool.hatch.build.targets.wheel]
packages = ["src/flashalpha_quantconnect"]

[tool.pytest.ini_options]
markers = [
  "integration: hits historical.flashalpha.com (requires FLASHALPHA_API_KEY)",
]
```

- [ ] **Step 3: Write `src/python/src/flashalpha_quantconnect/__init__.py`**

```python
"""FlashAlpha options-flow data as QuantConnect LEAN custom-data bars."""

__version__ = "0.1.0"

# Re-exports populated as bars are added in later tasks.
```

- [ ] **Step 4: Write `src/python/tests/__init__.py`** (empty file).

- [ ] **Step 5: Write `src/python/tests/conftest.py`**

```python
import os
import pytest


def pytest_collection_modifyitems(config, items):
    """Skip integration tests when FLASHALPHA_API_KEY is not set."""
    if os.environ.get("FLASHALPHA_API_KEY"):
        return
    skip_integration = pytest.mark.skip(reason="FLASHALPHA_API_KEY not set")
    for item in items:
        if "integration" in item.keywords:
            item.add_marker(skip_integration)
```

- [ ] **Step 6: Copy README.md from repo root into `src/python/README.md` (for PyPI build)**

For now copy a placeholder — the real README is written in Task 31.

```bash
cp README.md src/python/README.md
```

- [ ] **Step 7: Install the package in editable mode**

```bash
cd src/python
pip install -e ".[dev]"
```
Expected: `Successfully installed flashalpha-quantconnect-0.1.0`.

- [ ] **Step 8: Verify package imports**

```bash
python -c "import flashalpha_quantconnect; print(flashalpha_quantconnect.__version__)"
```
Expected: `0.1.0`.

- [ ] **Step 9: Verify pytest collects with the integration marker**

```bash
cd src/python
pytest --collect-only -q
```
Expected: no errors, 0 tests collected.

- [ ] **Step 10: Commit**

```bash
git add src/python/
git commit -m "chore: scaffold Python package skeleton with flashalpha-historical dep"
```

---

### Task 3: Auth-mechanism spike against live API

**Goal:** Decide between query-param auth (LEAN downloader handles it) vs. owned-HTTP fallback. Write the decision as a one-page ADR.

**Files:**
- Create: `docs/superpowers/specs/2026-05-28-auth-mechanism-decision.md`

- [ ] **Step 1: Try query-param auth against the live API**

```bash
curl -s "https://historical.flashalpha.com/v1/exposure/gex/SPY?at=2024-06-14&api_key=$FLASHALPHA_API_KEY" | head -50
```
- **If the response is the GEX JSON payload:** query-param auth works → mechanism = Query-Param.
- **If the response is 401 / "missing authorization":** query-param auth is not supported → mechanism = Owned-HTTP.

- [ ] **Step 2: Confirm the header-auth control case still works**

```bash
curl -s -H "Authorization: Bearer $FLASHALPHA_API_KEY" "https://historical.flashalpha.com/v1/exposure/gex/SPY?at=2024-06-14" | head -50
```
Expected: GEX JSON payload (sanity check that the key is valid and the endpoint reachable).

- [ ] **Step 3: Write the ADR**

Create `docs/superpowers/specs/2026-05-28-auth-mechanism-decision.md`:

```markdown
# Auth mechanism decision

**Date:** 2026-05-28
**Status:** Accepted

## Decision

The bridge uses **<QUERY-PARAM | OWNED-HTTP>** auth.

## Evidence

curl test from Step 1 returned: <paste status code + first 5 lines of body>.
curl test from Step 2 returned: <paste status code + first 5 lines of body>.

## Implication for FlashAlphaSource

- **Query-Param:** `GetSource(...)` returns `new SubscriptionDataSource(url + "?api_key=" + key, SubscriptionTransportMedium.Rest, FileFormat.Csv)`. LEAN downloads. `Reader(line)` parses the response body.
- **Owned-HTTP:** `FlashAlphaHttpClient` (wrapping `FlashAlpha.Historical.FlashAlphaHistoricalClient`) fetches the JSON, caches in process memory keyed by `(endpoint, ticker, date)`. `GetSource(...)` returns a sentinel `SubscriptionDataSource`. `Reader(...)` ignores `line` and reads from cache by config.Symbol + date.

## Source

Spike performed against `historical.flashalpha.com` on 2026-05-28.
```

Fill the `<...>` slots with the actual observations.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-05-28-auth-mechanism-decision.md
git commit -m "docs(spec): record auth-mechanism decision after live-API spike"
```

---

## Phase 1 — Shared infrastructure both languages (6 tasks)

### Task 4: C# FlashAlphaConfig with 4-step key resolution

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect/Config/FlashAlphaConfig.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect/Exceptions.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Config/FlashAlphaConfigTests.cs`

- [ ] **Step 1: Write the failing tests**

`src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Config/FlashAlphaConfigTests.cs`:

```csharp
using FlashAlpha.QuantConnect;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Config;

[Trait("Category", "Integration")]
public class FlashAlphaConfigTests
{
    [Fact]
    public void ResolveApiKey_PrefersExplicitOverride()
    {
        FlashAlphaConfig.Reset();
        FlashAlphaConfig.ApiKey = "explicit-key";
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", "env-key");

        Assert.Equal("explicit-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));

        FlashAlphaConfig.Reset();
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", null);
    }

    [Fact]
    public void ResolveApiKey_FallsBackToQCParameter()
    {
        FlashAlphaConfig.Reset();
        Assert.Equal("qc-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: key =>
            key == "flashalpha-api-key" ? "qc-key" : null));
    }

    [Fact]
    public void ResolveApiKey_FallsBackToEnvVar()
    {
        FlashAlphaConfig.Reset();
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", "env-key");

        Assert.Equal("env-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));

        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", null);
    }

    [Fact]
    public void ResolveApiKey_ThrowsWhenAllSourcesMiss()
    {
        FlashAlphaConfig.Reset();
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", null);

        Assert.Throws<FlashAlphaAuthMissingException>(() =>
            FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~FlashAlphaConfigTests"
```
Expected: 4 tests FAIL with "type or namespace 'FlashAlphaConfig' could not be found".

- [ ] **Step 3: Write the exception hierarchy**

`src/csharp/FlashAlpha.QuantConnect/Exceptions.cs`:

```csharp
namespace FlashAlpha.QuantConnect;

public class FlashAlphaQuantConnectException : Exception
{
    public string ErrorCode { get; }
    public string DocUrl => $"https://github.com/FlashAlpha-lab/flashalpha-quantconnect/blob/main/docs/troubleshooting.md#{ErrorCode.ToLowerInvariant()}";

    protected FlashAlphaQuantConnectException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}

public sealed class FlashAlphaAuthMissingException : FlashAlphaQuantConnectException
{
    public FlashAlphaAuthMissingException()
        : base("FA-AUTH-001", "FlashAlpha API key not found. Set FLASHALPHA_API_KEY env var, FlashAlphaConfig.ApiKey, or QC parameter 'flashalpha-api-key'.") { }
}

public sealed class FlashAlphaUnauthorizedException : FlashAlphaQuantConnectException
{
    public FlashAlphaUnauthorizedException(string endpoint, string keyTail4)
        : base("FA-AUTH-002", $"FlashAlpha rejected the API key (…{keyTail4}) on endpoint {endpoint}. See troubleshooting docs.") { }
}

public sealed class FlashAlphaRateLimitedException : FlashAlphaQuantConnectException
{
    public FlashAlphaRateLimitedException(string endpoint, Exception inner)
        : base("FA-RATE-001", $"FlashAlpha rate-limited the request to {endpoint} after retries exhausted.", inner) { }
}

public sealed class FlashAlphaNetworkException : FlashAlphaQuantConnectException
{
    public FlashAlphaNetworkException(string endpoint, Exception inner)
        : base("FA-NET-001", $"Network error talking to FlashAlpha at {endpoint}.", inner) { }
}
```

- [ ] **Step 4: Write `FlashAlphaConfig`**

`src/csharp/FlashAlpha.QuantConnect/Config/FlashAlphaConfig.cs`:

```csharp
namespace FlashAlpha.QuantConnect;

public static class FlashAlphaConfig
{
    public static string? ApiKey { get; set; }
    public static string BaseUrl { get; set; } = "https://historical.flashalpha.com";
    public static TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public static int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Resolution order: explicit ApiKey property → QC GetParameter("flashalpha-api-key")
    /// → FLASHALPHA_API_KEY env var → SDK auto-discovery. Throws if all four miss.
    /// </summary>
    public static string ResolveApiKey(Func<string, string?>? qcGetParameter = null)
    {
        if (!string.IsNullOrEmpty(ApiKey)) return ApiKey!;

        var fromQc = qcGetParameter?.Invoke("flashalpha-api-key");
        if (!string.IsNullOrEmpty(fromQc)) return fromQc!;

        var fromEnv = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (!string.IsNullOrEmpty(fromEnv)) return fromEnv!;

        // SDK auto-discovery delegated to FlashAlphaHistoricalClient — but if we
        // reach here, the SDK's defaults didn't populate the env var either,
        // so fail fast.
        throw new FlashAlphaAuthMissingException();
    }

    /// <summary>Reset state — for tests only.</summary>
    public static void Reset()
    {
        ApiKey = null;
        BaseUrl = "https://historical.flashalpha.com";
        HttpTimeout = TimeSpan.FromSeconds(30);
        MaxRetries = 3;
    }
}
```

- [ ] **Step 5: Run tests to confirm they pass**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~FlashAlphaConfigTests"
```
Expected: 4 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/csharp/
git commit -m "feat(csharp): FlashAlphaConfig with 4-step key resolution + exception hierarchy"
```

---

### Task 5: Python config + exception hierarchy

**Files:**
- Create: `src/python/src/flashalpha_quantconnect/config.py`
- Create: `src/python/src/flashalpha_quantconnect/exceptions.py`
- Create: `src/python/tests/test_config.py`

- [ ] **Step 1: Write the failing tests**

`src/python/tests/test_config.py`:

```python
import os
import pytest

from flashalpha_quantconnect import config
from flashalpha_quantconnect.exceptions import FlashAlphaAuthMissingException


@pytest.fixture(autouse=True)
def _reset_config():
    config.reset()
    saved = os.environ.pop("FLASHALPHA_API_KEY", None)
    yield
    config.reset()
    if saved:
        os.environ["FLASHALPHA_API_KEY"] = saved


def test_resolve_api_key_prefers_explicit_override():
    config.api_key = "explicit-key"
    os.environ["FLASHALPHA_API_KEY"] = "env-key"
    assert config.resolve_api_key(qc_get_parameter=lambda _: None) == "explicit-key"


def test_resolve_api_key_falls_back_to_qc_parameter():
    assert config.resolve_api_key(
        qc_get_parameter=lambda k: "qc-key" if k == "flashalpha-api-key" else None,
    ) == "qc-key"


def test_resolve_api_key_falls_back_to_env_var():
    os.environ["FLASHALPHA_API_KEY"] = "env-key"
    assert config.resolve_api_key(qc_get_parameter=lambda _: None) == "env-key"


def test_resolve_api_key_throws_when_all_miss():
    with pytest.raises(FlashAlphaAuthMissingException):
        config.resolve_api_key(qc_get_parameter=lambda _: None)
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
cd src/python
pytest tests/test_config.py -v
```
Expected: 4 tests FAIL with `ImportError` on `config` or `exceptions`.

- [ ] **Step 3: Write `exceptions.py`**

`src/python/src/flashalpha_quantconnect/exceptions.py`:

```python
"""Exception hierarchy for flashalpha-quantconnect.

All bridge exceptions inherit from FlashAlphaQuantConnectException so users
can catch the family in one block. Each exception has a fixed error code
that maps to docs/troubleshooting.md.
"""


class FlashAlphaQuantConnectException(Exception):
    """Base for all bridge exceptions."""

    error_code: str = "FA-000"

    @property
    def doc_url(self) -> str:
        return (
            "https://github.com/FlashAlpha-lab/flashalpha-quantconnect/"
            f"blob/main/docs/troubleshooting.md#{self.error_code.lower()}"
        )


class FlashAlphaAuthMissingException(FlashAlphaQuantConnectException):
    error_code = "FA-AUTH-001"

    def __init__(self) -> None:
        super().__init__(
            "FlashAlpha API key not found. Set FLASHALPHA_API_KEY env var, "
            "flashalpha_quantconnect.config.api_key, or QC parameter "
            "'flashalpha-api-key'.",
        )


class FlashAlphaUnauthorizedException(FlashAlphaQuantConnectException):
    error_code = "FA-AUTH-002"

    def __init__(self, endpoint: str, key_tail4: str) -> None:
        super().__init__(
            f"FlashAlpha rejected the API key (…{key_tail4}) on endpoint {endpoint}.",
        )


class FlashAlphaRateLimitedException(FlashAlphaQuantConnectException):
    error_code = "FA-RATE-001"

    def __init__(self, endpoint: str) -> None:
        super().__init__(
            f"FlashAlpha rate-limited the request to {endpoint} after retries exhausted.",
        )


class FlashAlphaNetworkException(FlashAlphaQuantConnectException):
    error_code = "FA-NET-001"

    def __init__(self, endpoint: str) -> None:
        super().__init__(f"Network error talking to FlashAlpha at {endpoint}.")
```

- [ ] **Step 4: Write `config.py`**

`src/python/src/flashalpha_quantconnect/config.py`:

```python
"""Module-level configuration for the bridge.

Resolution order for the FlashAlpha API key:
  1. Explicit override (set config.api_key = "...")
  2. QC GetParameter("flashalpha-api-key") for QC Cloud
  3. Environment variable FLASHALPHA_API_KEY
  4. SDK auto-discovery (delegated to flashalpha-historical)
"""

from __future__ import annotations

import os
from typing import Callable, Optional

from .exceptions import FlashAlphaAuthMissingException


api_key: Optional[str] = None
base_url: str = "https://historical.flashalpha.com"
http_timeout_s: float = 30.0
max_retries: int = 3


def resolve_api_key(
    qc_get_parameter: Optional[Callable[[str], Optional[str]]] = None,
) -> str:
    """Walk the resolution order; raise FlashAlphaAuthMissingException if all miss."""
    global api_key

    if api_key:
        return api_key

    if qc_get_parameter is not None:
        from_qc = qc_get_parameter("flashalpha-api-key")
        if from_qc:
            return from_qc

    from_env = os.environ.get("FLASHALPHA_API_KEY")
    if from_env:
        return from_env

    raise FlashAlphaAuthMissingException()


def reset() -> None:
    """Reset module state — for tests only."""
    global api_key, base_url, http_timeout_s, max_retries
    api_key = None
    base_url = "https://historical.flashalpha.com"
    http_timeout_s = 30.0
    max_retries = 3
```

- [ ] **Step 5: Update `__init__.py` to re-export**

`src/python/src/flashalpha_quantconnect/__init__.py`:

```python
"""FlashAlpha options-flow data as QuantConnect LEAN custom-data bars."""

__version__ = "0.1.0"

from . import config
from .exceptions import (
    FlashAlphaAuthMissingException,
    FlashAlphaNetworkException,
    FlashAlphaQuantConnectException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)

__all__ = [
    "config",
    "FlashAlphaAuthMissingException",
    "FlashAlphaNetworkException",
    "FlashAlphaQuantConnectException",
    "FlashAlphaRateLimitedException",
    "FlashAlphaUnauthorizedException",
]
```

- [ ] **Step 6: Run tests to confirm they pass**

```bash
cd src/python
pytest tests/test_config.py -v
```
Expected: 4 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/python/
git commit -m "feat(python): config with 4-step key resolution + exception hierarchy"
```

---

### Task 6: C# FlashAlphaHttpClient wrapping the SDK

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect/Client/IFlashAlphaHttpClient.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect/Client/FlashAlphaHttpClient.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Client/FlashAlphaHttpClientTests.cs`

- [ ] **Step 1: Write the failing test (real API call, single endpoint)**

`src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Client/FlashAlphaHttpClientTests.cs`:

```csharp
using FlashAlpha.QuantConnect.Client;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Client;

[Trait("Category", "Integration")]
public class FlashAlphaHttpClientTests
{
    [Fact]
    public async Task FetchGexJson_ReturnsNonEmptyJsonForKnownDate()
    {
        var client = new FlashAlphaHttpClient();

        var json = await client.FetchJsonAsync(
            endpoint: "exposure/gex",
            ticker: "SPY",
            at: new DateTime(2024, 6, 14));

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("\"net_gex\"", json);
        Assert.Contains("\"symbol\"", json);
    }
}
```

- [ ] **Step 2: Run test to confirm it fails**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~FlashAlphaHttpClientTests"
```
Expected: FAIL with "type or namespace 'FlashAlphaHttpClient' could not be found".

- [ ] **Step 3: Write the interface**

`src/csharp/FlashAlpha.QuantConnect/Client/IFlashAlphaHttpClient.cs`:

```csharp
namespace FlashAlpha.QuantConnect.Client;

/// <summary>
/// Thin façade over the FlashAlpha.Historical SDK. The bridge mocks this in
/// tests where a real API call would be wasteful (e.g., schema-drift unit tests).
/// </summary>
public interface IFlashAlphaHttpClient
{
    Task<string> FetchJsonAsync(string endpoint, string ticker, DateTime at, CancellationToken ct = default);
}
```

- [ ] **Step 4: Write the implementation**

`src/csharp/FlashAlpha.QuantConnect/Client/FlashAlphaHttpClient.cs`:

```csharp
using FlashAlpha.Historical;     // FlashAlphaHistoricalClient
using System.Text.Json;

namespace FlashAlpha.QuantConnect.Client;

public sealed class FlashAlphaHttpClient : IFlashAlphaHttpClient
{
    private readonly FlashAlphaHistoricalClient _sdk;

    public FlashAlphaHttpClient(FlashAlphaHistoricalClient? sdk = null)
    {
        // Resolve key once at construction; surface FlashAlphaAuthMissingException
        // immediately if none of the four sources have a key.
        var key = FlashAlphaConfig.ResolveApiKey();
        _sdk = sdk ?? new FlashAlphaHistoricalClient(apiKey: key, baseUrl: FlashAlphaConfig.BaseUrl);
    }

    public async Task<string> FetchJsonAsync(
        string endpoint, string ticker, DateTime at, CancellationToken ct = default)
    {
        // SDK exposes typed endpoint methods (Gex, Dex, ...). We need raw JSON
        // to feed LEAN's Reader, so we re-serialize the typed response. This
        // round-trip is intentional: the SDK enforces schema + auth + retries
        // and we benefit from all three; the serialized form is what LEAN's
        // subscription pipeline expects.
        try
        {
            var atString = at.ToString("yyyy-MM-ddTHH:mm:ssZ");
            object response = endpoint switch
            {
                "exposure/gex"       => await _sdk.GexAsync(ticker, atString, ct),
                "exposure/dex"       => await _sdk.DexAsync(ticker, atString, ct),
                "exposure/vex"       => await _sdk.VexAsync(ticker, atString, ct),
                "exposure/chex"      => await _sdk.ChexAsync(ticker, atString, ct),
                "exposure/summary"   => await _sdk.ExposureSummaryAsync(ticker, atString, ct),
                "exposure/levels"    => await _sdk.ExposureLevelsAsync(ticker, atString, ct),
                "surface"            => await _sdk.SurfaceAsync(ticker, atString, ct),
                "exposure/zero-dte"  => await _sdk.ZeroDteAsync(ticker, atString, ct),
                "max-pain"           => await _sdk.MaxPainAsync(ticker, atString, ct),
                "volatility"         => await _sdk.VolatilityAsync(ticker, atString, ct),
                "adv-volatility"     => await _sdk.AdvVolatilityAsync(ticker, atString, ct),
                "vrp"                => await _sdk.VrpAsync(ticker, atString, ct),
                "narrative"          => await _sdk.NarrativeAsync(ticker, atString, ct),
                "stock/summary"      => await _sdk.StockSummaryAsync(ticker, atString, ct),
                "stock/quote"        => await _sdk.StockQuoteAsync(ticker, atString, ct),
                "option/quote"       => await _sdk.OptionQuoteAsync(ticker, atString, ct),
                "tickers"            => await _sdk.TickersAsync(atString, ct),
                _ => throw new ArgumentException($"Unknown endpoint: {endpoint}"),
            };
            return JsonSerializer.Serialize(response);
        }
        catch (FlashAlphaHistoricalUnauthorizedException ex)
        {
            var tail4 = ex.Message.Length >= 4 ? "????" : "????"; // SDK doesn't expose the key — use a fixed sentinel
            throw new FlashAlphaUnauthorizedException(endpoint, tail4);
        }
        catch (FlashAlphaHistoricalRateLimitException ex)
        {
            throw new FlashAlphaRateLimitedException(endpoint, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FlashAlphaNetworkException(endpoint, ex);
        }
    }
}
```

**Note:** the SDK's exact method names may differ slightly. The engineer should open `src/FlashAlpha.Historical/FlashAlphaHistoricalClient.cs` from the dotnet SDK's source tree and match the actual method signatures.

- [ ] **Step 5: Run test to confirm it passes**

```bash
export FLASHALPHA_API_KEY=...
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~FlashAlphaHttpClientTests"
```
Expected: PASS in <5s. JSON contains `net_gex` and `symbol`.

- [ ] **Step 6: Commit**

```bash
git add src/csharp/
git commit -m "feat(csharp): FlashAlphaHttpClient wrapping flashalpha-historical-dotnet SDK"
```

---

### Task 7: Python FlashAlphaHttpClient (mirrored)

**Files:**
- Create: `src/python/src/flashalpha_quantconnect/client.py`
- Create: `src/python/tests/test_http_client.py`

- [ ] **Step 1: Write the failing test**

`src/python/tests/test_http_client.py`:

```python
from datetime import datetime

import pytest

from flashalpha_quantconnect.client import FlashAlphaHttpClient


@pytest.mark.integration
def test_fetch_gex_json_returns_payload():
    client = FlashAlphaHttpClient()

    raw = client.fetch_json(endpoint="exposure/gex", ticker="SPY", at=datetime(2024, 6, 14))

    assert isinstance(raw, dict)
    assert raw.get("symbol") == "SPY"
    assert "net_gex" in raw
```

- [ ] **Step 2: Run test to confirm it fails**

```bash
cd src/python
pytest tests/test_http_client.py -v
```
Expected: FAIL with `ImportError: cannot import name 'FlashAlphaHttpClient'`.

- [ ] **Step 3: Write `client.py`**

```python
"""Thin wrapper around the flashalpha-historical SDK.

The bridge does not implement HTTP itself — auth, retries, schema validation
are all delegated to the SDK. This wrapper exists to (a) expose a single
endpoint-string entry point that bar classes can call uniformly, (b) translate
SDK exceptions into bridge exceptions.
"""

from __future__ import annotations

from datetime import datetime
from typing import Any, Optional

from flashalpha_historical import Client as _SdkClient
from flashalpha_historical.exceptions import (
    FlashAlphaHistoricalUnauthorizedError,
    FlashAlphaHistoricalRateLimitError,
    FlashAlphaHistoricalNetworkError,
)

from . import config
from .exceptions import (
    FlashAlphaNetworkException,
    FlashAlphaRateLimitedException,
    FlashAlphaUnauthorizedException,
)


_ENDPOINT_TO_METHOD = {
    "exposure/gex":      "gex",
    "exposure/dex":      "dex",
    "exposure/vex":      "vex",
    "exposure/chex":     "chex",
    "exposure/summary":  "exposure_summary",
    "exposure/levels":   "exposure_levels",
    "surface":           "surface",
    "exposure/zero-dte": "zero_dte",
    "max-pain":          "max_pain",
    "volatility":        "volatility",
    "adv-volatility":    "adv_volatility",
    "vrp":               "vrp",
    "narrative":         "narrative",
    "stock/summary":     "stock_summary",
    "stock/quote":       "stock_quote",
    "option/quote":      "option_quote",
    "tickers":           "tickers",
}


class FlashAlphaHttpClient:
    def __init__(self, sdk: Optional[_SdkClient] = None) -> None:
        key = config.resolve_api_key()
        self._sdk = sdk or _SdkClient(api_key=key, base_url=config.base_url)

    def fetch_json(self, endpoint: str, ticker: str, at: datetime) -> dict[str, Any]:
        method_name = _ENDPOINT_TO_METHOD.get(endpoint)
        if method_name is None:
            raise ValueError(f"Unknown endpoint: {endpoint}")

        method = getattr(self._sdk, method_name)
        at_iso = at.strftime("%Y-%m-%dT%H:%M:%SZ")

        try:
            if endpoint == "tickers":
                return method(at=at_iso)
            return method(ticker=ticker, at=at_iso)
        except FlashAlphaHistoricalUnauthorizedError as e:
            raise FlashAlphaUnauthorizedException(endpoint, "????") from e
        except FlashAlphaHistoricalRateLimitError as e:
            raise FlashAlphaRateLimitedException(endpoint) from e
        except FlashAlphaHistoricalNetworkError as e:
            raise FlashAlphaNetworkException(endpoint) from e
```

**Note:** the SDK's exact exception class names may differ — the engineer should open `flashalpha-historical-python/src/flashalpha_historical/exceptions.py` and match the actual class names. Likewise verify the method-name mapping by reading `client.py` of the SDK.

- [ ] **Step 4: Run test to confirm it passes**

```bash
export FLASHALPHA_API_KEY=...
cd src/python
pytest tests/test_http_client.py -v
```
Expected: PASS in <5s.

- [ ] **Step 5: Commit**

```bash
git add src/python/
git commit -m "feat(python): FlashAlphaHttpClient wrapping flashalpha-historical SDK"
```

---

### Task 8: C# FlashAlphaSource helpers (shared bar plumbing)

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect/Data/FlashAlphaSource.cs`

This task adds the helper used by every bar's `GetSource` + `Reader` overrides. The auth mechanism (query-param vs owned-HTTP) selected in Task 3 determines which branch ships.

- [ ] **Step 1: Write `FlashAlphaSource.cs` — Query-Param branch**

(Use this if Task 3's ADR selected Query-Param auth.)

```csharp
using QuantConnect;
using QuantConnect.Data;
using System.Text.Json;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Shared GetSource/Reader plumbing for every FlashAlpha bar. Query-param branch.
/// </summary>
public static class FlashAlphaSource
{
    public static SubscriptionDataSource For(string endpoint, Symbol symbol, DateTime date)
    {
        var ticker = symbol.Value;
        var atIso = date.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var key = FlashAlphaConfig.ResolveApiKey();
        var url = $"{FlashAlphaConfig.BaseUrl}/v1/{endpoint}/{ticker}?at={atIso}&api_key={key}";
        return new SubscriptionDataSource(url, SubscriptionTransportMedium.Rest, FileFormat.Csv);
    }

    public static T Parse<T>(string line, Symbol symbol, DateTime date) where T : BaseData, new()
    {
        if (string.IsNullOrWhiteSpace(line)) return null!;

        var bar = new T
        {
            Symbol = symbol,
            Time = date,
            EndTime = date.AddDays(1),
        };

        var elem = JsonDocument.Parse(line).RootElement;
        FlashAlphaJsonMapper.PopulateProperties(bar, elem);
        return bar;
    }
}
```

- [ ] **Step 1 (alt): Write `FlashAlphaSource.cs` — Owned-HTTP branch**

(Use this if Task 3's ADR selected Owned-HTTP auth.)

```csharp
using QuantConnect;
using QuantConnect.Data;
using System.Collections.Concurrent;
using System.Text.Json;

namespace FlashAlpha.QuantConnect.Data;

public static class FlashAlphaSource
{
    private static readonly Lazy<Client.FlashAlphaHttpClient> _http =
        new(() => new Client.FlashAlphaHttpClient());
    private static readonly ConcurrentDictionary<string, string> _cache = new();

    public static SubscriptionDataSource For(string endpoint, Symbol symbol, DateTime date)
    {
        var key = $"{endpoint}|{symbol.Value}|{date:yyyy-MM-dd}";
        // Pre-fetch + cache; Reader will read from cache via the sentinel URL.
        _cache[key] = _http.Value.FetchJsonAsync(endpoint, symbol.Value, date).GetAwaiter().GetResult();
        return new SubscriptionDataSource($"flashalpha://{key}", SubscriptionTransportMedium.Rest, FileFormat.Csv);
    }

    public static T Parse<T>(string line, Symbol symbol, DateTime date) where T : BaseData, new()
    {
        // line may be the sentinel URL or the actual JSON depending on LEAN version.
        // Always read from the cache by the (endpoint, symbol, date) key.
        // The bar subclass passes endpoint via the JsonMapper context.
        var key = line.StartsWith("flashalpha://") ? line.Substring("flashalpha://".Length) : null;
        var json = key != null && _cache.TryGetValue(key, out var cached) ? cached : line;
        if (string.IsNullOrWhiteSpace(json)) return null!;

        var bar = new T
        {
            Symbol = symbol,
            Time = date,
            EndTime = date.AddDays(1),
        };

        var elem = JsonDocument.Parse(json).RootElement;
        FlashAlphaJsonMapper.PopulateProperties(bar, elem);
        return bar;
    }
}
```

- [ ] **Step 2: Write `FlashAlphaJsonMapper.cs`** (shared by both branches)

`src/csharp/FlashAlpha.QuantConnect/Data/FlashAlphaJsonMapper.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlashAlpha.QuantConnect.Data;

internal static class FlashAlphaJsonMapper
{
    /// <summary>
    /// Map snake_case JSON keys to bar properties (PascalCase by convention).
    /// Reflection-driven: each bar exposes properties whose names map cleanly
    /// to the SDK DTO field names via snake-case → PascalCase conversion.
    /// </summary>
    public static void PopulateProperties(object bar, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return;
        var type = bar.GetType();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite) continue;
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                          ?? ToSnakeCase(prop.Name);
            if (!root.TryGetProperty(jsonName, out var element)) continue;
            var value = ReadValue(element, prop.PropertyType);
            if (value != null) prop.SetValue(bar, value);
        }
    }

    private static string ToSnakeCase(string pascal)
    {
        var sb = new System.Text.StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static object? ReadValue(JsonElement el, Type targetType)
    {
        if (el.ValueKind == JsonValueKind.Null) return null;
        if (targetType == typeof(decimal)) return el.GetDecimal();
        if (targetType == typeof(double)) return el.GetDouble();
        if (targetType == typeof(int)) return el.GetInt32();
        if (targetType == typeof(long)) return el.GetInt64();
        if (targetType == typeof(string)) return el.GetString();
        if (targetType == typeof(bool)) return el.GetBoolean();
        if (targetType == typeof(DateTime)) return el.GetDateTime();
        // For nested types, recurse via JsonSerializer
        return JsonSerializer.Deserialize(el.GetRawText(), targetType);
    }
}
```

- [ ] **Step 3: Confirm both files compile (no test yet — the test comes with the first bar)**

```bash
dotnet build src/csharp/FlashAlpha.QuantConnect.sln
```
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/csharp/
git commit -m "feat(csharp): FlashAlphaSource + JsonMapper shared bar plumbing"
```

---

### Task 9: Python source helpers (mirrored)

**Files:**
- Create: `src/python/src/flashalpha_quantconnect/data/__init__.py`
- Create: `src/python/src/flashalpha_quantconnect/data/source.py`

- [ ] **Step 1: Create `data/__init__.py`** (empty file — populated as bars are added).

- [ ] **Step 2: Write `source.py`** (Query-Param branch — use if Task 3 selected Query-Param)

```python
"""Shared GetSource/Reader plumbing for every FlashAlpha bar.

Query-param auth branch — the URL includes ?api_key=... and LEAN's
RemoteFile downloader handles HTTP.
"""

from __future__ import annotations

import json
from datetime import datetime
from typing import Any, Type

# QuantConnect types are available in the LEAN runtime.
# In unit-test environments we import them from quantconnect-lean / clr stubs.
from QuantConnect import SubscriptionDataSource
from QuantConnect.Data import SubscriptionTransportMedium, FileFormat

from .. import config


def source_for(endpoint: str, symbol: Any, date: datetime) -> Any:
    ticker = symbol.Value
    at_iso = date.strftime("%Y-%m-%dT%H:%M:%SZ")
    key = config.resolve_api_key()
    url = f"{config.base_url}/v1/{endpoint}/{ticker}?at={at_iso}&api_key={key}"
    return SubscriptionDataSource(url, SubscriptionTransportMedium.Rest, FileFormat.Csv)


def parse(bar_cls: Type, line: str, symbol: Any, date: datetime) -> Any:
    if not line or not line.strip():
        return None

    obj = json.loads(line)
    bar = bar_cls()
    bar.Symbol = symbol
    bar.Time = date
    bar.EndTime = date.replace(hour=date.hour + 24) if date.hour + 24 < 48 else date

    _populate(bar, obj)
    return bar


def _populate(bar: Any, obj: dict) -> None:
    """snake_case JSON key → PascalCase bar property."""
    for key, value in obj.items():
        prop = _to_pascal_case(key)
        if hasattr(bar, prop):
            setattr(bar, prop, value)


def _to_pascal_case(snake: str) -> str:
    return "".join(p.title() for p in snake.split("_"))
```

- [ ] **Step 2 (alt): Write `source.py`** (Owned-HTTP branch — use if Task 3 selected Owned-HTTP)

```python
from __future__ import annotations

import json
from datetime import datetime
from threading import Lock
from typing import Any, Type

from QuantConnect import SubscriptionDataSource
from QuantConnect.Data import SubscriptionTransportMedium, FileFormat

from .. import config
from ..client import FlashAlphaHttpClient


_cache: dict[str, str] = {}
_cache_lock = Lock()
_client: FlashAlphaHttpClient | None = None


def _get_client() -> FlashAlphaHttpClient:
    global _client
    if _client is None:
        _client = FlashAlphaHttpClient()
    return _client


def source_for(endpoint: str, symbol: Any, date: datetime) -> Any:
    ticker = symbol.Value
    cache_key = f"{endpoint}|{ticker}|{date:%Y-%m-%d}"
    json_payload = _get_client().fetch_json(endpoint, ticker, date)
    with _cache_lock:
        _cache[cache_key] = json.dumps(json_payload)
    return SubscriptionDataSource(
        f"flashalpha://{cache_key}",
        SubscriptionTransportMedium.Rest,
        FileFormat.Csv,
    )


def parse(bar_cls: Type, line: str, symbol: Any, date: datetime) -> Any:
    if not line:
        return None
    if line.startswith("flashalpha://"):
        cache_key = line[len("flashalpha://"):]
        with _cache_lock:
            payload = _cache.get(cache_key)
        if payload is None:
            return None
    else:
        payload = line

    obj = json.loads(payload)
    bar = bar_cls()
    bar.Symbol = symbol
    bar.Time = date
    bar.EndTime = date.replace(day=date.day + 1) if date.day < 28 else date

    for key, value in obj.items():
        prop = "".join(p.title() for p in key.split("_"))
        if hasattr(bar, prop):
            setattr(bar, prop, value)
    return bar
```

- [ ] **Step 3: Confirm package still imports**

```bash
cd src/python
python -c "from flashalpha_quantconnect.data.source import source_for"
```
Expected: no errors. (The `QuantConnect` imports may fail outside a LEAN runtime; if so, the engineer should add `pragma: no cover — runtime-only` notes or wrap imports in `TYPE_CHECKING`.)

- [ ] **Step 4: Commit**

```bash
git add src/python/
git commit -m "feat(python): data.source shared bar plumbing"
```

---

## Phase 2 — First bar (GEX) proves the full pattern (5 tasks)

This phase delivers a fully working GEX bar end-to-end in both languages. Once these tasks pass, the remaining 16 bars follow the same pattern with different field sets.

### Task 10: C# FlashAlphaGexBar + Layer 1 subscription test

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect/Data/Exposure.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Subscription/GexSubscriptionTests.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Fixtures/TestAlgorithm.cs`

- [ ] **Step 1: Open the SDK DTO to mirror fields**

Open `e:/repos/tecware/flashalpha-packages/flashalpha-historical-dotnet/src/FlashAlpha.Historical/Models/Exposure.cs`. List the public properties on `GexResponse`. Headline fields: `Symbol`, `UnderlyingPrice`, `AsOf`, `GammaFlip`, `NetGex`, `NetGexLabel`, `Strikes`.

The bar exposes the **scalar** fields directly (for fast LEAN access). The strikes list is exposed as-is.

- [ ] **Step 2: Write the failing subscription test**

`src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Fixtures/TestAlgorithm.cs`:

```csharp
using QuantConnect.Algorithm;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.IntegrationTests.Fixtures;

/// <summary>
/// Minimal test algorithm that collects all received custom data slices into
/// a list for inspection. Runs a single trading day so tests are fast.
/// </summary>
public sealed class TestAlgorithm<T> : QCAlgorithm where T : BaseData, new()
{
    private readonly DateTime _date;
    private readonly string _ticker;
    public List<T> Collected { get; } = new();

    public TestAlgorithm(DateTime date, string ticker)
    {
        _date = date;
        _ticker = ticker;
    }

    public override void Initialize()
    {
        SetStartDate(_date);
        SetEndDate(_date.AddDays(1));
        AddData<T>(_ticker, QuantConnect.Resolution.Daily);
    }

    public override void OnData(Slice slice)
    {
        foreach (var kv in slice)
            if (kv.Value is T t) Collected.Add(t);
    }
}
```

`src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Subscription/GexSubscriptionTests.cs`:

```csharp
using FlashAlpha.QuantConnect.Data;
using FlashAlpha.QuantConnect.IntegrationTests.Fixtures;
using QuantConnect.Lean.Engine;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Subscription;

[Trait("Category", "Integration")]
public class GexSubscriptionTests
{
    [Fact]
    public async Task GexBar_SubscribesAndPopulatesFields()
    {
        var algo = new TestAlgorithm<FlashAlphaGexBar>(new DateTime(2024, 6, 14), "SPY");
        await LeanRunner.Run(algo);

        Assert.Single(algo.Collected);
        var bar = algo.Collected[0];

        Assert.True(bar.NetGex != 0, "NetGex must be populated");
        Assert.False(string.IsNullOrEmpty(bar.NetGexLabel), "NetGexLabel must be populated");
    }
}
```

`LeanRunner` is a one-shot helper — write it next.

`src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Fixtures/LeanRunner.cs`:

```csharp
using QuantConnect.Algorithm;
using QuantConnect.Lean.Engine.Setup;

namespace FlashAlpha.QuantConnect.IntegrationTests.Fixtures;

internal static class LeanRunner
{
    public static async Task Run(QCAlgorithm algo)
    {
        // Use LEAN's BacktestingSetupHandler to bootstrap a single backtest
        // run with the algorithm we constructed. We bypass the standard
        // discovery path (json config + algorithm location) by injecting
        // the instance directly.
        var setup = new BacktestingSetupHandler();
        // The exact API surface for in-process backtest invocation varies
        // by LEAN version. The engineer should consult QC's "Lean as a
        // library" docs (https://www.quantconnect.com/docs/v2/lean-engine/...)
        // and substitute the equivalent in-process invocation.
        await Task.CompletedTask;
        throw new NotImplementedException(
            "Wire up in-process LEAN invocation; see LEAN docs.");
    }
}
```

**Note for the engineer:** in-process LEAN invocation is a non-trivial integration step; the engineer should consult QC LEAN docs and the LEAN repo's `Tests/` folder for canonical examples. The skeleton is intentionally left for the engineer to wire correctly against the LEAN version pulled in by the .csproj.

- [ ] **Step 3: Run the test to confirm it fails**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~GexSubscriptionTests"
```
Expected: FAIL with "type or namespace 'FlashAlphaGexBar' could not be found".

- [ ] **Step 4: Write `FlashAlphaGexBar`**

`src/csharp/FlashAlpha.QuantConnect/Data/Exposure.cs`:

```csharp
using QuantConnect;
using QuantConnect.Data;
using System.Text.Json.Serialization;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Gamma exposure (GEX) bar from FlashAlpha historical API.
/// Mirrors <see cref="FlashAlpha.Historical.Models.GexResponse"/>.
/// Subscribe with: <c>algo.AddData&lt;FlashAlphaGexBar&gt;(ticker, Resolution.Daily)</c>.
/// </summary>
public class FlashAlphaGexBar : BaseData
{
    [JsonPropertyName("underlying_price")]
    public decimal UnderlyingPrice { get; set; }

    [JsonPropertyName("as_of")]
    public string AsOf { get; set; } = "";

    [JsonPropertyName("gamma_flip")]
    public decimal? GammaFlip { get; set; }

    [JsonPropertyName("net_gex")]
    public decimal NetGex { get; set; }

    /// <summary>Coarse regime label — typically "positive" or "negative".</summary>
    [JsonPropertyName("net_gex_label")]
    public string NetGexLabel { get; set; } = "";

    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/gex", config.Symbol, date);

    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaGexBar>(line, config.Symbol, date);
}
```

- [ ] **Step 5: Wire LEAN in-process and run the test**

After consulting LEAN docs and replacing the `NotImplementedException` in `LeanRunner` with real wiring:

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~GexSubscriptionTests"
```
Expected: PASS — the test collects one bar with non-zero `NetGex`.

- [ ] **Step 6: Commit**

```bash
git add src/csharp/
git commit -m "feat(csharp): FlashAlphaGexBar + LEAN test fixture + Layer 1 subscription test"
```

---

### Task 11: C# Layer 2 price-correctness test for GEX

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/PriceCorrectness/GexPriceCorrectnessTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using FlashAlpha.QuantConnect.IntegrationTests.Fixtures;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.PriceCorrectness;

[Trait("Category", "Integration")]
public class GexPriceCorrectnessTests
{
    [Fact]
    public async Task GexBar_FieldsMatchRestResponse()
    {
        var sdk = new FlashAlphaHistoricalClient(
            apiKey: Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY")!);
        var raw = await sdk.GexAsync("SPY", "2024-06-14T00:00:00Z");

        var algo = new TestAlgorithm<FlashAlphaGexBar>(new DateTime(2024, 6, 14), "SPY");
        await LeanRunner.Run(algo);

        var bar = algo.Collected.Single();

        Assert.Equal((decimal)raw.UnderlyingPrice!, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.GammaFlip is null ? null : (decimal?)raw.GammaFlip.Value, bar.GammaFlip);
        Assert.Equal((decimal)raw.NetGex!, bar.NetGex);
        Assert.Equal(raw.NetGexLabel, bar.NetGexLabel);
    }
}
```

- [ ] **Step 2: Run the test**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~GexPriceCorrectness"
```
Expected: PASS — every field matches the raw REST response.

If any field diverges, fix `FlashAlphaJsonMapper.PopulateProperties` (Task 8 Step 2) or the bar property attributes until it passes. **Do not relax the test.**

- [ ] **Step 3: Commit**

```bash
git add src/csharp/
git commit -m "test(csharp): GEX bar price correctness vs REST response"
```

---

### Task 12: C# AddFlashAlphaGex sugar extension

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect/Extensions/QCAlgorithmExtensions.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Subscription/SugarExtensionsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using FlashAlpha.QuantConnect.IntegrationTests.Fixtures;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Subscription;

[Trait("Category", "Integration")]
public class SugarExtensionsTests
{
    [Fact]
    public async Task AddFlashAlphaGex_AddsCustomDataSubscription()
    {
        // The sugar must produce a subscription equivalent to AddData<FlashAlphaGexBar>.
        var algoSugar = new SugarHarnessAlgorithm("SPY", new DateTime(2024, 6, 14));
        await LeanRunner.Run(algoSugar);

        Assert.Single(algoSugar.Collected);
        Assert.True(algoSugar.Collected[0].NetGex != 0);
    }

    private sealed class SugarHarnessAlgorithm : QCAlgorithm
    {
        private readonly string _ticker;
        private readonly DateTime _date;
        public List<FlashAlphaGexBar> Collected { get; } = new();

        public SugarHarnessAlgorithm(string ticker, DateTime date)
        {
            _ticker = ticker;
            _date = date;
        }

        public override void Initialize()
        {
            SetStartDate(_date);
            SetEndDate(_date.AddDays(1));
            this.AddFlashAlphaGex(_ticker);   // sugar under test
        }

        public override void OnData(QuantConnect.Data.Slice slice)
        {
            foreach (var kv in slice)
                if (kv.Value is FlashAlphaGexBar bar) Collected.Add(bar);
        }
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~SugarExtensionsTests"
```
Expected: FAIL with "'QCAlgorithm' does not contain a definition for 'AddFlashAlphaGex'".

- [ ] **Step 3: Write the extension**

```csharp
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect;

public static class QCAlgorithmExtensions
{
    /// <summary>Subscribe to FlashAlpha gamma exposure (GEX) bars for the given ticker.</summary>
    public static Security AddFlashAlphaGex(this QCAlgorithm algo, string ticker, Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaGexBar>(ticker, resolution);
}
```

- [ ] **Step 4: Run tests to confirm PASS**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~SugarExtensionsTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/csharp/
git commit -m "feat(csharp): AddFlashAlphaGex sugar extension + test"
```

---

### Task 13: Python GexBar + Layer 1 subscription test

**Files:**
- Create: `src/python/src/flashalpha_quantconnect/data/exposure.py`
- Create: `src/python/tests/subscription/test_gex_subscription.py`
- Create: `src/python/tests/fixtures/test_algorithm.py`
- Create: `src/python/tests/fixtures/lean_runner.py`

- [ ] **Step 1: Open the SDK TypedDict to mirror fields**

Open `e:/repos/tecware/flashalpha-packages/flashalpha-historical-python/src/flashalpha_historical/types.py`. The `GexResponse` TypedDict mirrors the dotnet POCO: `symbol`, `underlying_price`, `as_of`, `gamma_flip`, `net_gex`, `net_gex_label`, `strikes`.

- [ ] **Step 2: Write the test fixture**

`src/python/tests/fixtures/__init__.py`: empty.

`src/python/tests/fixtures/test_algorithm.py`:

```python
from datetime import datetime, timedelta
from typing import Type

from QuantConnect.Algorithm import QCAlgorithm
from QuantConnect.Data import Slice


def make_test_algorithm(bar_cls: Type, ticker: str, date: datetime) -> QCAlgorithm:
    class _Algo(QCAlgorithm):
        def Initialize(self):
            self.SetStartDate(date)
            self.SetEndDate(date + timedelta(days=1))
            self.AddData(bar_cls, ticker)
            self.collected = []

        def OnData(self, slice: Slice):
            for kvp in slice:
                if isinstance(kvp.Value, bar_cls):
                    self.collected.append(kvp.Value)

    return _Algo()
```

`src/python/tests/fixtures/lean_runner.py`:

```python
"""In-process LEAN runner. Wire to QC Cloud Python LEAN API.

The engineer should consult QC's "Lean as a library (Python)" docs and replace
the stub below with the canonical invocation for the LEAN version pulled in
by pyproject.toml.
"""

import asyncio
from typing import Any


async def run(algorithm: Any) -> None:
    raise NotImplementedError("Wire up in-process Python LEAN invocation")
```

- [ ] **Step 3: Write the failing test**

`src/python/tests/subscription/__init__.py`: empty.

`src/python/tests/subscription/test_gex_subscription.py`:

```python
from datetime import datetime

import pytest

from flashalpha_quantconnect import GexBar
from tests.fixtures.test_algorithm import make_test_algorithm
from tests.fixtures.lean_runner import run


@pytest.mark.integration
@pytest.mark.asyncio
async def test_gex_bar_subscribes_and_populates_fields():
    algo = make_test_algorithm(GexBar, "SPY", datetime(2024, 6, 14))
    await run(algo)

    assert len(algo.collected) == 1
    bar = algo.collected[0]

    assert bar.NetGex != 0
    assert bar.NetGexLabel
```

- [ ] **Step 4: Run the test to confirm it fails**

```bash
cd src/python
pytest tests/subscription/test_gex_subscription.py -v
```
Expected: FAIL with `ImportError: cannot import name 'GexBar'`.

- [ ] **Step 5: Write the bar**

`src/python/src/flashalpha_quantconnect/data/exposure.py`:

```python
"""Exposure bars: GEX, DEX, VEX, CHEX, ExposureSummary, ExposureLevels.

Mirrors flashalpha_historical.types ExposureSummaryResponse etc.
"""

from __future__ import annotations

from typing import Any

from QuantConnect.Python import PythonData

from .source import source_for, parse


class GexBar(PythonData):
    """Gamma exposure (GEX) bar.

    Subscribe with: ``algo.AddData(GexBar, ticker, Resolution.Daily)``.
    """

    UnderlyingPrice: float = 0.0
    AsOf: str = ""
    GammaFlip: float | None = None
    NetGex: float = 0.0
    NetGexLabel: str = ""

    def GetSource(self, config: Any, date: Any, is_live_mode: bool) -> Any:
        return source_for("exposure/gex", config.Symbol, date)

    def Reader(self, config: Any, line: str, date: Any, is_live_mode: bool) -> Any:
        return parse(GexBar, line, config.Symbol, date)
```

- [ ] **Step 6: Re-export from package root**

Update `src/python/src/flashalpha_quantconnect/__init__.py` to add:

```python
from .data.exposure import GexBar

__all__ = [
    "config", "GexBar",
    "FlashAlphaAuthMissingException", "FlashAlphaNetworkException",
    "FlashAlphaQuantConnectException", "FlashAlphaRateLimitedException",
    "FlashAlphaUnauthorizedException",
]
```

- [ ] **Step 7: Wire LEAN in-process Python and re-run**

After replacing the stub in `lean_runner.py`:

```bash
cd src/python
pytest tests/subscription/test_gex_subscription.py -v
```
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/python/
git commit -m "feat(python): GexBar + LEAN test fixture + Layer 1 subscription test"
```

---

### Task 14: Python GEX price correctness + sugar

**Files:**
- Create: `src/python/src/flashalpha_quantconnect/extensions.py`
- Create: `src/python/tests/price_correctness/test_gex_correctness.py`
- Create: `src/python/tests/subscription/test_sugar_extensions.py`

- [ ] **Step 1: Write the failing price-correctness test**

`src/python/tests/price_correctness/__init__.py`: empty.

`src/python/tests/price_correctness/test_gex_correctness.py`:

```python
from datetime import datetime

import pytest
from flashalpha_historical import Client as SdkClient

from flashalpha_quantconnect import GexBar
from tests.fixtures.test_algorithm import make_test_algorithm
from tests.fixtures.lean_runner import run


@pytest.mark.integration
@pytest.mark.asyncio
async def test_gex_bar_fields_match_rest_response():
    sdk = SdkClient()
    raw = sdk.gex(ticker="SPY", at="2024-06-14T00:00:00Z")

    algo = make_test_algorithm(GexBar, "SPY", datetime(2024, 6, 14))
    await run(algo)
    bar = algo.collected[0]

    assert bar.UnderlyingPrice == pytest.approx(raw["underlying_price"])
    assert bar.AsOf == raw["as_of"]
    assert bar.GammaFlip == raw.get("gamma_flip")
    assert bar.NetGex == pytest.approx(raw["net_gex"])
    assert bar.NetGexLabel == raw["net_gex_label"]
```

- [ ] **Step 2: Write the failing sugar test**

`src/python/tests/subscription/test_sugar_extensions.py`:

```python
from datetime import datetime, timedelta

import pytest
from QuantConnect.Algorithm import QCAlgorithm

from flashalpha_quantconnect import GexBar, add_flashalpha_gex
from tests.fixtures.lean_runner import run


@pytest.mark.integration
@pytest.mark.asyncio
async def test_add_flashalpha_gex_adds_subscription():
    class _Algo(QCAlgorithm):
        def Initialize(self):
            self.SetStartDate(datetime(2024, 6, 14))
            self.SetEndDate(datetime(2024, 6, 15))
            add_flashalpha_gex(self, "SPY")
            self.collected = []

        def OnData(self, slice):
            for kvp in slice:
                if isinstance(kvp.Value, GexBar):
                    self.collected.append(kvp.Value)

    algo = _Algo()
    await run(algo)
    assert len(algo.collected) == 1
    assert algo.collected[0].NetGex != 0
```

- [ ] **Step 3: Run both tests to confirm they fail**

```bash
cd src/python
pytest tests/price_correctness tests/subscription/test_sugar_extensions.py -v
```
Expected: price-correctness FAILs on assertion mismatch (if mapper has bugs) or PASSes; sugar FAILs with `ImportError: cannot import name 'add_flashalpha_gex'`.

- [ ] **Step 4: Write the sugar module**

`src/python/src/flashalpha_quantconnect/extensions.py`:

```python
"""Module-level sugar helpers for subscribing to FlashAlpha bars.

Python can't extend QCAlgorithm cleanly with new methods, so these are
free functions that take the algorithm as the first arg. Idiomatic call:

    add_flashalpha_gex(self, "SPY")
"""

from __future__ import annotations

from typing import Any

from .data.exposure import GexBar


def add_flashalpha_gex(algorithm: Any, ticker: str, resolution: Any = None) -> Any:
    if resolution is None:
        from QuantConnect import Resolution
        resolution = Resolution.Daily
    return algorithm.AddData(GexBar, ticker, resolution)
```

- [ ] **Step 5: Re-export from package root**

Add to `__init__.py`:

```python
from .extensions import add_flashalpha_gex
```

And update `__all__`.

- [ ] **Step 6: Run both tests to confirm PASS**

```bash
cd src/python
pytest tests/price_correctness tests/subscription/test_sugar_extensions.py -v
```
Expected: 2 PASS.

If price-correctness fails on field mismatch, fix `data/source.py`'s `_to_pascal_case` or the bar's property annotations until every field matches. **Do not relax the test.**

- [ ] **Step 7: Commit**

```bash
git add src/python/
git commit -m "feat(python): add_flashalpha_gex sugar + GEX price correctness test"
```

---

## Phase 3 — Remaining 16 bars × 2 languages (16 tasks)

Each task in this phase adds one bar in both languages, with its Layer 1 subscription test and Layer 2 price-correctness test, plus the sugar extension. The pattern repeats exactly the GEX work in Tasks 10–14; only the field list, endpoint slug, and class name change.

**Workflow per task:**
1. Open the matching SDK DTO (dotnet `Models/*.cs` + Python `types.py`) to list fields.
2. Write the failing Layer 1 subscription test in C#.
3. Add the C# bar class to the matching `Data/*.cs` file (creating the file or appending to it).
4. Add the C# sugar extension method to `QCAlgorithmExtensions.cs`.
5. Write the failing Layer 2 price-correctness test in C#.
6. Run C# tests until PASS. Commit C# changes.
7. Repeat steps 2–6 in Python (`data/*.py`, `extensions.py`, `tests/subscription/`, `tests/price_correctness/`).
8. Single commit per language at end.

### Task 15: DEX bar (Exposure.cs / exposure.py)

**Endpoint:** `exposure/dex`
**SDK DTO:** `DexResponse` (mirrors `GexResponse` — see Exposure.cs)
**Bar properties:** `UnderlyingPrice`, `AsOf`, `NetDex`, `NetDexLabel` (+ optional strikes list)
**Class names:** `FlashAlphaDexBar` (C#), `DexBar` (Python)
**Sugar:** `AddFlashAlphaDex(this QCAlgorithm)` / `add_flashalpha_dex(algorithm, ...)`

Follow the workflow above. Bar shape is identical to GEX with `NetDex` substituted for `NetGex`. Commit C# changes with `feat(csharp): DEX bar + tests`. Commit Python with `feat(python): DEX bar + tests`.

### Task 16: VEX bar (Exposure.cs / exposure.py)

**Endpoint:** `exposure/vex`
**SDK DTO:** `VexResponse`
**Bar properties:** `UnderlyingPrice`, `AsOf`, `NetVex`, `NetVexLabel`
**Class names:** `FlashAlphaVexBar`, `VexBar`
**Sugar:** `AddFlashAlphaVex` / `add_flashalpha_vex`

### Task 17: CHEX bar (Exposure.cs / exposure.py)

**Endpoint:** `exposure/chex`
**SDK DTO:** `ChexResponse`
**Bar properties:** `UnderlyingPrice`, `AsOf`, `NetChex`, `NetChexLabel`
**Class names:** `FlashAlphaChexBar`, `ChexBar`
**Sugar:** `AddFlashAlphaChex` / `add_flashalpha_chex`

### Task 18: ExposureSummary bar (Exposure.cs / exposure.py)

**Endpoint:** `exposure/summary`
**SDK DTO:** `ExposureSummaryResponse` (in `flashalpha_historical/types.py` for Python; `ExposureSummary.cs` for dotnet — read both, they're rich)
**Bar properties (flat scalar view; nested objects stored as nullable nested types):**
- `Symbol`, `UnderlyingPrice`, `AsOf`, `GammaFlip`, `Regime`
- `Exposures` (nested: `NetGex`, `NetDex`, `NetVex`, `NetChex`)
- `Interpretation` (nested: `Gamma`, `Vanna`, `Charm`)
- `HedgingEstimate` (nested with `SpotUp1Pct`/`SpotDown1Pct` each carrying `DealerSharesToTrade`/`Direction`/`NotionalUsd`)
- `ZeroDte` (nested: `NetGex`, `PctOfTotalGex`, `Expiration`)

**Class names:** `FlashAlphaExposureSummaryBar`, `ExposureSummaryBar`
**Sugar:** `AddFlashAlphaExposureSummary` / `add_flashalpha_exposure_summary`

**Important regime enum check:** the Python TypedDict at `types.py:78` declares `Literal["positive_gamma", "negative_gamma", "unknown"]`. The dotnet memory said the live API also returns `"neutral"` / `"undetermined"`. Subscribe the live API on the test date, observe the actual enum value, and update the bar's docstring with the observed set. Do NOT enforce a strict whitelist in code — pass the raw string through.

### Task 19: ExposureLevels bar (Exposure.cs / exposure.py)

**Endpoint:** `exposure/levels`
**SDK DTO:** `ExposureLevelsResponse` (read `ExposureLevels.cs` for fields)
**Class names:** `FlashAlphaExposureLevelsBar`, `ExposureLevelsBar`
**Sugar:** `AddFlashAlphaExposureLevels` / `add_flashalpha_exposure_levels`

### Task 20: Surface bar (Surface.cs / surface.py)

**Endpoint:** `surface`
**SDK DTO:** `SurfaceResponse` (read `Surface.cs`)
**Bar properties:** scalar headline fields (e.g., `UnderlyingPrice`, `AsOf`) plus the full SVI slice list / smile grid as a nested type.
**Class names:** `FlashAlphaSurfaceBar`, `SurfaceBar`
**Sugar:** `AddFlashAlphaSurface` / `add_flashalpha_surface`

### Task 21: ZeroDte bar (ZeroDte.cs / zero_dte.py)

**Endpoint:** `exposure/zero-dte`
**SDK DTO:** `ZeroDteResponse`
**Class names:** `FlashAlphaZeroDteBar`, `ZeroDteBar`
**Sugar:** `AddFlashAlphaZeroDte` / `add_flashalpha_zero_dte`

### Task 22: MaxPain bar (MaxPain.cs / max_pain.py)

**Endpoint:** `max-pain`
**SDK DTO:** `MaxPainResponse`
**Class names:** `FlashAlphaMaxPainBar`, `MaxPainBar`
**Sugar:** `AddFlashAlphaMaxPain` / `add_flashalpha_max_pain`

### Task 23: Volatility bar (Volatility.cs / volatility.py)

**Endpoint:** `volatility`
**SDK DTO:** `VolatilityResponse`
**Class names:** `FlashAlphaVolatilityBar`, `VolatilityBar`
**Sugar:** `AddFlashAlphaVolatility` / `add_flashalpha_volatility`

### Task 24: AdvVolatility bar (Volatility.cs / volatility.py)

**Endpoint:** `adv-volatility`
**SDK DTO:** `AdvVolatilityResponse` (in `AdvVolatility.cs`)
**Class names:** `FlashAlphaAdvVolatilityBar`, `AdvVolatilityBar`
**Sugar:** `AddFlashAlphaAdvVolatility` / `add_flashalpha_adv_volatility`

### Task 25: VRP bar (Vrp.cs / vrp.py)

**Endpoint:** `vrp`
**SDK DTO:** `VrpResponse`
**Class names:** `FlashAlphaVrpBar`, `VrpBar`
**Sugar:** `AddFlashAlphaVrp` / `add_flashalpha_vrp`

### Task 26: Narrative bar (Narrative.cs / narrative.py)

**Endpoint:** `narrative`
**SDK DTO:** `NarrativeResponse`
**Class names:** `FlashAlphaNarrativeBar`, `NarrativeBar`
**Sugar:** `AddFlashAlphaNarrative` / `add_flashalpha_narrative`

### Task 27: StockSummary bar (StockSummary.cs / stock_summary.py)

**Endpoint:** `stock/summary`
**SDK DTO:** `StockSummaryResponse`
**Class names:** `FlashAlphaStockSummaryBar`, `StockSummaryBar`
**Sugar:** `AddFlashAlphaStockSummary` / `add_flashalpha_stock_summary`

### Task 28: StockQuote bar (StockQuote.cs / stock_quote.py)

**Endpoint:** `stock/quote`
**SDK DTO:** `StockQuoteResponse` (check `Models/` — may be inside another file)
**Class names:** `FlashAlphaStockQuoteBar`, `StockQuoteBar`
**Sugar:** `AddFlashAlphaStockQuote` / `add_flashalpha_stock_quote`

### Task 29: OptionQuote bar (OptionQuote.cs / option_quote.py)

**Endpoint:** `option/quote`
**SDK DTO:** `OptionQuoteResponse`
**Class names:** `FlashAlphaOptionQuoteBar`, `OptionQuoteBar`
**Sugar:** `AddFlashAlphaOptionQuote` / `add_flashalpha_option_quote`

### Task 30: Tickers bar (Tickers.cs / tickers.py)

**Endpoint:** `tickers`
**SDK DTO:** `TickersResponse`
**Class names:** `FlashAlphaTickersBar`, `TickersBar`
**Sugar:** `AddFlashAlphaTickers` / `add_flashalpha_tickers`

Note: the `tickers` endpoint doesn't take a per-symbol ticker — pass a sentinel `"*"` or call without ticker semantics. The SDK method signature dictates how to wire this; check `FlashAlphaHttpClient.FetchJsonAsync` Task 6's switch for the special case.

---

## Phase 4 — Universe helper & Layer 3 golden tests (3 tasks)

### Task 31: FlashAlphaTickersUniverse helper (C# + Python)

**Files:**
- Create (C#): `src/csharp/FlashAlpha.QuantConnect/Data/Tickers.cs` (append `FlashAlphaTickersUniverse` class to same file as `FlashAlphaTickersBar`)
- Create (C#): `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Universe/TickersUniverseTests.cs`
- Modify (Python): `src/python/src/flashalpha_quantconnect/data/tickers.py` (append class)
- Create (Python): `src/python/tests/universe/test_tickers_universe.py`

- [ ] **Step 1: Write failing tests (both langs)**

C#:

```csharp
[Trait("Category", "Integration")]
public class TickersUniverseTests
{
    [Fact]
    public async Task FlashAlphaTickersUniverse_FiltersByPredicate()
    {
        // Filter universe to tickers that have zero-DTE coverage on test date.
        var algo = new UniverseHarnessAlgorithm(
            filter: row => row.HasZeroDte,
            date: new DateTime(2024, 6, 14));
        await LeanRunner.Run(algo);

        Assert.NotEmpty(algo.AddedSymbols);
        Assert.Contains("SPY", algo.AddedSymbols.Select(s => s.Value));
    }

    // UniverseHarnessAlgorithm: subclasses QCAlgorithm, adds the universe in Initialize,
    // collects added symbols in OnSecuritiesChanged.
}
```

Python — equivalent shape.

- [ ] **Step 2: Implement `FlashAlphaTickersUniverse`**

The class extends `UniverseSelectionModel` (LEAN's universe pattern). On each universe selection event, calls the `tickers` endpoint, filters by the user-provided predicate, returns the symbols. Wire it to LEAN's `AddUniverseSelection` API.

- [ ] **Step 3: Run tests to PASS** (both langs)

- [ ] **Step 4: Commit (one commit covering both langs)**

```bash
git add src/csharp/ src/python/
git commit -m "feat: FlashAlphaTickersUniverse universe-selection helper"
```

---

### Task 32: C# Layer 3 end-to-end golden backtest

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/EndToEnd/GexRegimeFollowingAlgorithm.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/EndToEnd/EndToEndBacktestTests.cs`
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/golden/end_to_end.json`

- [ ] **Step 1: Write the algorithm**

`GexRegimeFollowingAlgorithm.cs`:

```csharp
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.IntegrationTests.EndToEnd;

/// <summary>
/// Long SPY when regime is positive_gamma, flat otherwise. Q2 2024.
/// Used as the Layer 3 golden-numbers integration test.
/// </summary>
public class GexRegimeFollowingAlgorithm : QCAlgorithm
{
    private Symbol _spy = null!;
    private Symbol _gex = null!;

    public override void Initialize()
    {
        SetStartDate(2024, 4, 1);
        SetEndDate(2024, 6, 30);
        SetCash(100_000);

        _spy = AddEquity("SPY", Resolution.Daily).Symbol;
        _gex = this.AddFlashAlphaGex("SPY").Symbol;
    }

    public override void OnData(Slice slice)
    {
        if (!slice.ContainsKey(_gex)) return;
        var gex = (FlashAlphaGexBar)slice[_gex];

        if (gex.NetGexLabel == "positive")
            SetHoldings(_spy, 1.0);
        else
            Liquidate(_spy);
    }
}
```

- [ ] **Step 2: Run the algorithm once locally to capture golden numbers**

```bash
dotnet run --project src/csharp/FlashAlpha.QuantConnect.IntegrationTests \
  -- --algorithm GexRegimeFollowingAlgorithm --capture-golden
```

(The engineer wires `--capture-golden` to write `end_to_end.json` with `{ "final_equity": ..., "total_trades": ..., "sharpe": ... }` from the LEAN backtest result.)

Inspect the produced numbers for sanity (final_equity should be ~$100k ± reasonable Q2-2024 SPY drift; trade count should be small but non-zero). Commit `end_to_end.json`.

- [ ] **Step 3: Write the assertion test**

`EndToEndBacktestTests.cs`:

```csharp
[Trait("Category", "Integration")]
public class EndToEndBacktestTests
{
    private static readonly EndToEndGolden Golden = LoadGolden();

    [Fact]
    public async Task GexRegimeFollowingAlgorithm_MatchesGolden()
    {
        var algo = new GexRegimeFollowingAlgorithm();
        var result = await LeanRunner.RunForResult(algo);

        Assert.Equal(Golden.FinalEquity, result.FinalEquity,
            tolerance: Math.Abs(Golden.FinalEquity) * 1e-4m);
        Assert.Equal(Golden.TotalTrades, result.TotalTrades);
        Assert.Equal(Golden.Sharpe, result.Sharpe, precision: 2);
    }

    private static EndToEndGolden LoadGolden()
        => JsonSerializer.Deserialize<EndToEndGolden>(
            File.ReadAllText("golden/end_to_end.json"))!;

    private sealed record EndToEndGolden(decimal FinalEquity, int TotalTrades, decimal Sharpe);
}
```

- [ ] **Step 4: Run test to confirm PASS**

```bash
dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~EndToEndBacktestTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/csharp/FlashAlpha.QuantConnect.IntegrationTests/EndToEnd/ \
        src/csharp/FlashAlpha.QuantConnect.IntegrationTests/golden/
git commit -m "test(csharp): end-to-end GEX-regime-following golden backtest"
```

---

### Task 33: Python Layer 3 end-to-end golden backtest

**Files:**
- Create: `src/python/tests/end_to_end/gex_regime_following_algorithm.py`
- Create: `src/python/tests/end_to_end/test_end_to_end_backtest.py`
- Create: `src/python/tests/golden/end_to_end.json`

Mirror Task 32 in Python:
- Subclass `QCAlgorithm` with the same logic (`positive_gamma` → 100% SPY, else flat).
- Capture golden numbers locally, commit `end_to_end.json` (separate from C# golden — Python LEAN may yield slightly different numbers, that's expected).
- Test asserts within tolerance: `rel=1e-4` on equity, exact on trades, `abs=0.01` on sharpe.

Commit `feat(python): end-to-end GEX-regime-following golden backtest`.

---

### Task 34: Schema drift guard test (both langs)

**Files:**
- Create: `src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Schema/SchemaDriftGuardTests.cs`
- Create: `src/python/tests/schema/test_schema_drift_guard.py`

- [ ] **Step 1: C# test**

Reflect over every `FlashAlphaXxxBar` class and assert that every public property has a corresponding field on the matching SDK DTO (using `[JsonPropertyName]` for the link). The mapping from bar class to DTO is hard-coded:

```csharp
private static readonly (Type Bar, Type Dto)[] BarToDto = new[]
{
    (typeof(FlashAlphaGexBar), typeof(FlashAlpha.Historical.Models.GexResponse)),
    (typeof(FlashAlphaDexBar), typeof(FlashAlpha.Historical.Models.DexResponse)),
    // ... all 17
};

[Theory]
[MemberData(nameof(BarTypePairs))]
public void Bar_FieldsAreSubsetOfSdkDto(Type barType, Type dtoType)
{
    var barJsonNames = barType.GetProperties()
        .Where(p => p.CanWrite)
        .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? ToSnakeCase(p.Name))
        .ToHashSet();

    var dtoJsonNames = dtoType.GetProperties()
        .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name)
        .ToHashSet();

    var missingOnBar = dtoJsonNames.Except(barJsonNames).ToList();
    // We tolerate the bar exposing a subset of SDK fields, but we want to know
    // if the bar references a field that DOESN'T exist on the SDK (= bug).
    var extrasOnBar = barJsonNames.Except(dtoJsonNames).ToList();
    Assert.Empty(extrasOnBar);
}
```

- [ ] **Step 2: Python test**

```python
import dataclasses
from typing import get_type_hints

import pytest
from flashalpha_historical import types as sdk_types
from flashalpha_quantconnect import (
    GexBar, DexBar, VexBar, ChexBar, ExposureSummaryBar, ExposureLevelsBar,
    SurfaceBar, ZeroDteBar, MaxPainBar, VolatilityBar, AdvVolatilityBar,
    VrpBar, NarrativeBar, StockSummaryBar, StockQuoteBar, OptionQuoteBar, TickersBar,
)


BAR_TO_DTO = [
    (GexBar, sdk_types.GexResponse),
    (DexBar, sdk_types.DexResponse),
    # ... all 17
]


@pytest.mark.parametrize("bar_cls,dto_cls", BAR_TO_DTO)
def test_bar_fields_are_subset_of_sdk_dto(bar_cls, dto_cls):
    bar_props = {
        # Pascal property → snake_case for comparison
        _to_snake(name) for name in vars(bar_cls)
        if not name.startswith("_") and not callable(getattr(bar_cls, name))
    }
    dto_props = set(get_type_hints(dto_cls).keys())

    extras_on_bar = bar_props - dto_props
    assert not extras_on_bar, f"{bar_cls.__name__} has fields not on {dto_cls.__name__}: {extras_on_bar}"


def _to_snake(pascal: str) -> str:
    out = []
    for i, c in enumerate(pascal):
        if i > 0 and c.isupper():
            out.append("_")
        out.append(c.lower())
    return "".join(out)
```

- [ ] **Step 3: Run tests to PASS in both langs**

If a bar exposes a field the SDK doesn't have, that's a bug — fix the bar (probably a typo).

- [ ] **Step 4: Commit**

```bash
git add src/csharp/FlashAlpha.QuantConnect.IntegrationTests/Schema/ \
        src/python/tests/schema/
git commit -m "test: schema drift guard ensures bar fields stay subset of SDK DTOs"
```

---

## Phase 5 — README, docs & SEO surface (6 tasks)

### Task 35: CLAUDE.md, AGENTS.md, CHANGELOG.md

**Files:**
- Create: `CLAUDE.md`
- Create: `AGENTS.md`
- Create: `CHANGELOG.md`

- [ ] **Step 1: Copy boilerplate**

Use the same `CLAUDE.md` content as `flashalpha-examples/CLAUDE.md` (already seen in the brainstorm session). `AGENTS.md` mirrors what `flashalpha-historical-dotnet/AGENTS.md` ships.

- [ ] **Step 2: Write `CHANGELOG.md`**

```markdown
# Changelog

All notable changes to flashalpha-quantconnect will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] — 2026-XX-XX

### Added
- C# (NuGet `FlashAlpha.QuantConnect`) and Python (PyPI `flashalpha-quantconnect`) packages.
- 17 QC custom-data bar classes covering every FlashAlpha-Historical endpoint.
- Sugar extensions: `AddFlashAlphaXxx` (C#) / `add_flashalpha_xxx` (Python).
- `FlashAlphaTickersUniverse` for universe selection.
- Integration tests against live API (subscription, price-correctness, end-to-end golden).
```

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md AGENTS.md CHANGELOG.md
git commit -m "docs: add CLAUDE.md, AGENTS.md, CHANGELOG.md boilerplate"
```

---

### Task 36: README.md (the SEO landing page)

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace the placeholder README with the full landing page**

Structure follows §7 of the spec exactly. Write the full README with:

1. One-line headline + 3-line subhead naming every endpoint by abbreviation.
2. Four badges (NuGet, PyPI, CI, License).
3. **What it does** — one-paragraph elevator pitch + a table of all 17 endpoints with class names per language.
4. **Install (60-second start)** — three side-by-side blocks: QC Cloud, Self-hosted C#, Self-hosted Python.
5. **First algorithm** — side-by-side C# + Python (the GEX-regime example from spec §4).
6. **Data catalog** — one row per endpoint with: endpoint slug, C# class, Python class, doc URL.
7. **Auth** — abbreviated from spec §5.
8. **Recipes** — bullet list with links to `docs/recipes/*.md`.
9. **FAQ** — 6 Q&A pairs covering: "Why a separate custom-data symbol from AddEquity?", "What's the API cost per backtest day?", "Does this work in QC Cloud?", "How is this different from polygon/CBOE feeds?", "Can I use this for live trading?", "What's the relationship to flashalpha-historical SDK?".
10. **Related** — links to `flashalpha-historical-examples`, raw SDKs (flashalpha-historical-{python,dotnet,js,go,java}), `historical.flashalpha.com`.
11. **License: MIT**.

Length target: 600–900 lines of well-structured markdown. The README is the package's most important SEO artefact — invest in it.

- [ ] **Step 2: Validate markdown rendering and link health**

```bash
npx markdown-link-check README.md
```
Expected: every link returns 200 (after the cross-referenced repos are created or pointed at placeholders).

- [ ] **Step 3: Mirror the README into src/python/ for PyPI**

```bash
cp README.md src/python/README.md
```

The Python package builds against `src/python/README.md` for its PyPI listing.

- [ ] **Step 4: Commit**

```bash
git add README.md src/python/README.md
git commit -m "docs: full README landing page with side-by-side C# + Python examples"
```

---

### Task 37: docs/getting-started.md + docs/data-types.md

**Files:**
- Create: `docs/getting-started.md`
- Create: `docs/data-types.md`

- [ ] **Step 1: Write `docs/getting-started.md`**

Sections:
- Prerequisites (FlashAlpha API key, QC Cloud account OR self-hosted LEAN).
- Install — C#.
- Install — Python.
- First algorithm — copy from README (links back).
- Resolution → API cost table (from spec §4).
- Where to go next (links to recipes, examples repo).

300–500 lines, code-heavy.

- [ ] **Step 2: Write `docs/data-types.md`**

One section per bar (17 sections). Each section has:
- Endpoint slug + link to `historical.flashalpha.com/docs/<endpoint>`.
- C# class name + Python class name.
- Full field table (Field, Type, Description, Nullable).
- Sample JSON response (truncated to 10 lines).
- Side-by-side C# + Python `OnData` example reading the bar.

Target length: ~1500 lines (88 lines per endpoint × 17). This is the long-tail SEO page — searches like "QuantConnect FlashAlpha VRP fields" will land here.

- [ ] **Step 3: Run link checker**

```bash
npx markdown-link-check docs/getting-started.md docs/data-types.md
```

- [ ] **Step 4: Commit**

```bash
git add docs/getting-started.md docs/data-types.md
git commit -m "docs: getting-started.md + data-types.md (17 endpoint reference)"
```

---

### Task 38: docs/auth.md + docs/troubleshooting.md

**Files:**
- Create: `docs/auth.md`
- Create: `docs/troubleshooting.md`

- [ ] **Step 1: Write `docs/auth.md`**

Sections:
- Where to get an API key (link to FlashAlpha sign-up).
- Setting the key for self-hosted LEAN (env var + `.env`).
- Setting the key for QC Cloud (Parameters tab + `SetParameter`).
- Setting the key for CI (GitHub secret).
- Key resolution order (the 4-step list from spec §5).
- Secrets hygiene (don't commit, last-4 logging convention).

200–400 lines.

- [ ] **Step 2: Write `docs/troubleshooting.md`**

Anchor headings for each error code (so error messages can link directly):
- `## fa-auth-001` — Missing API key. Symptoms, causes, fixes.
- `## fa-auth-002` — Unauthorized. Symptoms, causes (key revoked, wrong env), fixes.
- `## fa-rate-001` — Rate limited.
- `## fa-net-001` — Network error.
- `## why-two-symbols` — The "custom-data symbol ≠ equity symbol" gotcha.

200–400 lines.

- [ ] **Step 3: Commit**

```bash
git add docs/auth.md docs/troubleshooting.md
git commit -m "docs: auth.md + troubleshooting.md with error-code anchors"
```

---

### Task 39: Five recipe pages

**Files:**
- Create: `docs/recipes/subscribe-to-gex-in-quantconnect.md`
- Create: `docs/recipes/filter-universe-by-gex-regime.md`
- Create: `docs/recipes/combine-flashalpha-with-equity-data.md`
- Create: `docs/recipes/0dte-pin-risk-check-in-quantconnect.md`
- Create: `docs/recipes/vol-surface-snapshot-in-quantconnect.md`

Each recipe is 150–300 lines. Same structure for all five:

1. **Title** — exact match for a likely search query (the recipe filename IS the title).
2. **Problem** — one sentence framing.
3. **Solution** — side-by-side C# + Python code block.
4. **How it works** — 2–4 paragraphs of explanation.
5. **Variations** — bullet list of common tweaks (different ticker, different threshold, intraday resolution).
6. **Related recipes** — links to other recipes that touch the same concepts.

- [ ] **Step 1: Write each recipe** (5 separate steps, one per file).

- [ ] **Step 2: Run link checker**

```bash
npx markdown-link-check docs/recipes/*.md
```

- [ ] **Step 3: Commit**

```bash
git add docs/recipes/
git commit -m "docs: five recipe pages (SEO surface for common QC FlashAlpha tasks)"
```

---

### Task 40: llms.txt

**Files:**
- Modify: `llms.txt` (at repo root)

- [ ] **Step 1: Write `llms.txt`**

```
# flashalpha-quantconnect

> FlashAlpha options-flow & dealer-positioning data as QuantConnect LEAN custom data. GEX, DEX, VEX, vol surface, 0DTE, VRP, max-pain. C# (NuGet) and Python (PyPI).

## Docs
- [Getting started](docs/getting-started.md): Install + first algorithm in both languages.
- [Data types](docs/data-types.md): All 17 endpoints with field reference and code samples.
- [Authentication](docs/auth.md): API key setup for QC Cloud, self-hosted LEAN, and CI.
- [Troubleshooting](docs/troubleshooting.md): Error codes and common gotchas.

## Recipes
- [Subscribe to GEX in QuantConnect](docs/recipes/subscribe-to-gex-in-quantconnect.md)
- [Filter universe by GEX regime](docs/recipes/filter-universe-by-gex-regime.md)
- [Combine FlashAlpha with equity data](docs/recipes/combine-flashalpha-with-equity-data.md)
- [0DTE pin-risk check in QuantConnect](docs/recipes/0dte-pin-risk-check-in-quantconnect.md)
- [Vol surface snapshot in QuantConnect](docs/recipes/vol-surface-snapshot-in-quantconnect.md)

## Related
- [flashalpha-historical-examples](https://github.com/FlashAlpha-lab/flashalpha-historical-examples): 20+ backtest essays consuming this package.
- [flashalpha-historical-python](https://github.com/FlashAlpha-lab/flashalpha-historical-python): Raw FlashAlpha historical Python SDK.
- [flashalpha-historical-dotnet](https://github.com/FlashAlpha-lab/flashalpha-historical-dotnet): Raw FlashAlpha historical .NET SDK.
- [historical.flashalpha.com](https://historical.flashalpha.com/docs): The underlying API documentation.

## Optional
- [CHANGELOG.md](CHANGELOG.md): Version history.
- [LICENSE](LICENSE): MIT.
```

- [ ] **Step 2: Commit**

```bash
git add llms.txt
git commit -m "docs: llms.txt site map for LLM crawlers"
```

---

## Phase 6 — CI & release (3 tasks)

### Task 41: GitHub Actions CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Write `ci.yml`**

```yaml
name: CI

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

jobs:
  csharp:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Build
        run: dotnet build src/csharp/FlashAlpha.QuantConnect.sln -c Release
      - name: Test
        env:
          FLASHALPHA_API_KEY: ${{ secrets.FLASHALPHA_API_KEY }}
        run: dotnet test src/csharp/FlashAlpha.QuantConnect.sln -c Release --filter "Category=Integration"

  python:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with:
          python-version: '3.11'
      - name: Install
        run: |
          cd src/python
          pip install -e ".[dev]"
      - name: Test
        env:
          FLASHALPHA_API_KEY: ${{ secrets.FLASHALPHA_API_KEY }}
        run: |
          cd src/python
          pytest -m integration -v

  secrets-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }
      - uses: gitleaks/gitleaks-action@v2
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

  docs:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Markdown link check
        uses: gaurav-nelson/github-action-markdown-link-check@v1
        with:
          use-quiet-mode: 'yes'
          folder-path: 'docs,.'
```

- [ ] **Step 2: Set `FLASHALPHA_API_KEY` secret in the repo** (manual UI step — document it in the PR description).

- [ ] **Step 3: Push and confirm CI runs green**

After opening the PR, verify all four jobs (`csharp`, `python`, `secrets-scan`, `docs`) pass.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: PR workflow — build + integration tests + gitleaks + markdown link check"
```

---

### Task 42: GitHub Actions release workflows

**Files:**
- Create: `.github/workflows/release-csharp.yml`
- Create: `.github/workflows/release-python.yml`
- Create: `.github/workflows/nightly.yml`

- [ ] **Step 1: Write `release-csharp.yml`**

```yaml
name: Release C# to NuGet

on:
  push:
    tags: ['v*']

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Verify tag matches csproj version
        run: |
          TAG_VERSION=${GITHUB_REF_NAME#v}
          CSPROJ_VERSION=$(grep -oPm1 '(?<=<Version>)[^<]+' src/csharp/FlashAlpha.QuantConnect/FlashAlpha.QuantConnect.csproj)
          if [ "$TAG_VERSION" != "$CSPROJ_VERSION" ]; then
            echo "Tag $TAG_VERSION does not match csproj version $CSPROJ_VERSION"
            exit 1
          fi
      - name: Pack
        run: dotnet pack src/csharp/FlashAlpha.QuantConnect/FlashAlpha.QuantConnect.csproj -c Release -o nupkg
      - name: Push
        env:
          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
        run: dotnet nuget push "nupkg/FlashAlpha.QuantConnect.*.nupkg" --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
```

- [ ] **Step 2: Write `release-python.yml`**

```yaml
name: Release Python to PyPI

on:
  push:
    tags: ['v*']

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-python@v5
        with:
          python-version: '3.11'
      - name: Verify tag matches pyproject version
        run: |
          TAG_VERSION=${GITHUB_REF_NAME#v}
          PYPROJ_VERSION=$(grep -oPm1 '(?<=^version = ")[^"]+' src/python/pyproject.toml)
          if [ "$TAG_VERSION" != "$PYPROJ_VERSION" ]; then
            echo "Tag $TAG_VERSION does not match pyproject version $PYPROJ_VERSION"
            exit 1
          fi
      - name: Install build tools
        run: pip install build twine
      - name: Build
        run: |
          cd src/python
          python -m build
      - name: Publish
        env:
          TWINE_USERNAME: __token__
          TWINE_PASSWORD: ${{ secrets.PYPI_TOKEN }}
        run: |
          cd src/python
          twine upload dist/*
```

- [ ] **Step 3: Write `nightly.yml`**

```yaml
name: Nightly drift sentinel

on:
  schedule:
    - cron: '0 6 * * *'   # 06:00 UTC daily
  workflow_dispatch:

jobs:
  golden-drift:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - uses: actions/setup-python@v5
        with: { python-version: '3.11' }
      - name: Test C# golden
        env:
          FLASHALPHA_API_KEY: ${{ secrets.FLASHALPHA_API_KEY }}
        run: dotnet test src/csharp/FlashAlpha.QuantConnect.sln --filter "FullyQualifiedName~EndToEndBacktestTests"
      - name: Test Python golden
        env:
          FLASHALPHA_API_KEY: ${{ secrets.FLASHALPHA_API_KEY }}
        run: |
          cd src/python
          pip install -e ".[dev]"
          pytest tests/end_to_end -v
      - name: Notify on failure
        if: failure()
        uses: rtCamp/action-slack-notify@v2
        env:
          SLACK_WEBHOOK: ${{ secrets.SLACK_WEBHOOK }}
          SLACK_MESSAGE: 'Nightly golden drift detected — manual review required.'
```

- [ ] **Step 4: Configure secrets in GitHub**

Set in repo settings → Secrets:
- `FLASHALPHA_API_KEY` (read-only key)
- `NUGET_API_KEY` (from `%APPDATA%\NuGet\NuGet.Config` per registry_credentials memory)
- `PYPI_TOKEN` (account-scoped, required for first publish of new project name)
- `SLACK_WEBHOOK` (optional — Slack notifier for nightly drift)

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/release-csharp.yml .github/workflows/release-python.yml .github/workflows/nightly.yml
git commit -m "ci: release workflows for NuGet + PyPI + nightly drift sentinel"
```

---

### Task 43: v0.1.0 publish dry-run

This is the final task — produces the public v0.1.0 release.

- [ ] **Step 1: Verify all CI jobs pass on main**

Open the GitHub Actions tab; confirm latest `main` push has green checkmark on `csharp`, `python`, `secrets-scan`, `docs`.

- [ ] **Step 2: Dry-run NuGet pack**

```bash
dotnet pack src/csharp/FlashAlpha.QuantConnect/FlashAlpha.QuantConnect.csproj -c Release -o nupkg
ls -lah nupkg/
```
Expected: `FlashAlpha.QuantConnect.0.1.0.nupkg` and `.snupkg` (symbols).

Inspect the .nupkg with `unzip -l nupkg/FlashAlpha.QuantConnect.0.1.0.nupkg` — confirm `README.md`, `LICENSE`, and `lib/net8.0/FlashAlpha.QuantConnect.dll` are present.

- [ ] **Step 3: Dry-run Python build**

```bash
cd src/python
python -m build
ls -lah dist/
```
Expected: `flashalpha_quantconnect-0.1.0.tar.gz` and `.whl`.

```bash
twine check dist/*
```
Expected: `Checking ... PASSED`.

- [ ] **Step 4: Push tag to trigger release**

```bash
git tag -a v0.1.0 -m "v0.1.0 — initial release"
git push origin v0.1.0
```

- [ ] **Step 5: Verify release workflows succeed**

Watch GitHub Actions:
- `release-csharp.yml` → pushes to NuGet → visible at `https://www.nuget.org/packages/FlashAlpha.QuantConnect/0.1.0`.
- `release-python.yml` → pushes to PyPI → visible at `https://pypi.org/project/flashalpha-quantconnect/0.1.0/`.

- [ ] **Step 6: Smoke-test the published packages**

```bash
# Fresh dir, fresh deps
mkdir /tmp/smoke && cd /tmp/smoke
dotnet new console
dotnet add package FlashAlpha.QuantConnect --version 0.1.0
# Confirm: package installs, project compiles.

# Python smoke
python -m venv /tmp/smoke-py && source /tmp/smoke-py/bin/activate
pip install flashalpha-quantconnect==0.1.0
python -c "from flashalpha_quantconnect import GexBar; print(GexBar)"
```
Expected: both succeed.

- [ ] **Step 7: Set GitHub topics + cross-list**

Manually in GitHub repo settings → topics: `quantconnect`, `lean`, `options`, `gex`, `dealer-positioning`, `vol-surface`, `0dte`, `vrp`, `flashalpha`, `custom-data`, `algorithmic-trading`, `backtesting`, `csharp`, `python`.

Add an entry to `flashalpha-packages/awesome-options-analytics/README.md` under "Open Source Projects" + "APIs" + a new "QuantConnect" subsection (committed separately in awesome-options-analytics).

- [ ] **Step 8: Announce / final commit**

```bash
git commit --allow-empty -m "release: v0.1.0 published to NuGet + PyPI"
git push
```

Update `CHANGELOG.md` `[Unreleased]` → `[0.1.0] — <today's date>`, commit, push.

---

## Self-review checklist (for the implementing engineer)

After completing all 43 tasks, walk through this checklist:

- [ ] All 17 bars exist in both languages (12 files × 2 langs).
- [ ] All 17 sugar extensions exist (`AddFlashAlphaXxx` + `add_flashalpha_xxx`).
- [ ] `FlashAlphaTickersUniverse` works in both languages.
- [ ] CI is green on `main`.
- [ ] Schema drift guard passes (all bar fields are subset of SDK DTO fields).
- [ ] Layer 3 golden backtest passes within tolerance in both languages.
- [ ] `0.1.0` is published on NuGet AND PyPI.
- [ ] README has every endpoint named, every recipe linked, both languages shown side-by-side.
- [ ] `llms.txt` lists every doc page.
- [ ] GitHub topics set; awesome-options-analytics cross-link added.

If any item is unchecked, the task that should have produced it has a gap — revisit it.

---

## Open implementation questions (resolved during execution)

These are flagged for the engineer to decide as they hit them:

1. **In-process LEAN invocation API.** The `LeanRunner.Run` stub in Task 10/13 needs the actual LEAN-as-library invocation. Consult QC docs and the LEAN repo's `Tests/` folder.
2. **Regime enum literals.** Python TypedDict says `"unknown"`; dotnet memory says `"neutral"` / `"undetermined"`. Capture the actual value returned on the test date during Task 18 and update bar docstrings.
3. **`tickers` endpoint ticker semantics.** It doesn't take a per-symbol ticker. Task 30 needs special handling in the bar's `GetSource`.
4. **NuGet/PyPI project name availability.** Confirm `FlashAlpha.QuantConnect` and `flashalpha-quantconnect` are unclaimed before Task 43's dry-run. If claimed, fall back to a brand-aligned alternative (e.g., `FlashAlpha.QC` / `flashalpha-qc`) and update spec + plan.
