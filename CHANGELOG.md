# Changelog

All notable changes to `flashalpha-quantconnect` are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_No changes yet._

## [0.1.2] — 2026-06-01

### Fixed

- **`data.source.source_for` import path corrected.** v0.1.1 imported `SubscriptionDataSource` from `QuantConnect` directly — but in LEAN's runtime Python it lives at `QuantConnect.Data.SubscriptionDataSource`. Every Python consumer hit `cannot import name 'SubscriptionDataSource' from 'QuantConnect' (unknown location)` on the first bar subscription, which caused LEAN's data feed worker to silently drop the subscription. Algorithms would run to completion with 0 trades + flat equity, leaving the failure undetectable without inspecting LEAN's TRACE logs. Caught by Tier 2 smoke validation of the gamma-scalping essay in `flashalpha-historical-examples`; the fix is a one-liner consolidating the imports under `QuantConnect.Data`.

## [0.1.1] — 2026-05-30

### Fixed

- **`FlashAlphaJsonMapper` (C#) and `data.source.parse` (Python) no longer overwrite inherited LEAN properties from the JSON.** Previously the snake-case auto-mapper would walk every public property on the bar — including inherited `BaseData.Symbol` / `PythonData.Symbol` — and try to set them from JSON keys like `"symbol":"SPY"`. In C# this threw `JsonException` ("could not convert to `QuantConnect.Symbol`") at the very first parse; in Python it silently clobbered the QC Symbol object with the raw ticker string. The mapper now walks only attributes declared on the bar subclass (and any non-`QuantConnect.*` ancestors), so LEAN-owned surface is left alone. Caught by running v0.1.0 as a real NuGet consumer — surfaces on the first `FlashAlphaSource.Parse` against the live API.

## [0.1.0] — 2026-05-30

Initial public release. The bridge ships with full coverage of the FlashAlpha historical API surface as native QuantConnect LEAN custom-data bars, for both C# and Python.

### Added

- **C# package `FlashAlpha.QuantConnect` (NuGet).** Targets `netstandard2.0` for LEAN compatibility. Pulls typed response models from `FlashAlpha.Historical >= 0.4.0` so any upstream schema drift breaks at compile time.
- **Python package `flashalpha-quantconnect` (PyPI).** Requires Python 3.10+. Depends on `flashalpha-historical >= 0.4.0rc1`.
- **Seventeen custom-data bars**, one per FlashAlpha historical endpoint family — GEX, DEX, VEX, CHEX, exposure summary, exposure levels, vol surface, zero-DTE, max pain, volatility, advanced volatility (SVI/variance-swap/greeks surfaces), VRP, narrative, stock summary, stock quote, option quote, and tickers (coverage). Each bar mirrors its SDK response model one-for-one — same field names, same nullability, same shape.
- **Sugar extensions.** C# `QCAlgorithmExtensions.AddFlashAlpha*` methods on `QCAlgorithm`; Python module-level `add_flashalpha_*` helpers. One line per subscription, `Resolution.Daily` baked in as the default.
- **Universe-selection helper `FlashAlphaTickersUniverse`** in both languages. Backed by the `tickers` endpoint; takes a row-by-row predicate over `TickersRow` / `dict` so callers gate the universe on coverage span, healthy-day count, or any field the SDK exposes. Available without LEAN at construction time so unit tests can introspect the selected universe.
- **`FlashAlphaConfig` static (C#) / `flashalpha_quantconnect.config` module (Python).** API key resolution order: explicit override → QC `GetParameter("flashalpha-api-key")` → `FLASHALPHA_API_KEY` env var → throw `FlashAlphaAuthMissingException` (error code `FA-AUTH-001`).
- **Structured exceptions** — `FlashAlphaAuthMissingException` / `FlashAlphaUnauthorizedException` / `FlashAlphaRateLimitedException` / `FlashAlphaNetworkException`. Every exception carries a stable `ErrorCode` (`FA-AUTH-001` / `FA-AUTH-002` / `FA-RATE-001` / `FA-NET-001`) that doubles as the anchor in [docs/troubleshooting.md](docs/troubleshooting.md). Unauthorized exceptions log the last four characters of the key, never the full value.
- **Integration tests.** Pytest + xUnit suites hitting live `historical.flashalpha.com` behind a `FLASHALPHA_API_KEY` env-var guard.
- **SDK drift guard.** A CI job that pulls every typed-response field from the upstream `flashalpha-historical` SDK and asserts the corresponding bar declares the same property — so a silent SDK schema bump surfaces as a red CI run, not a silently corrupt bar at runtime.
- **Documentation corpus.** Repo-root `README.md` with side-by-side C# + Python examples, `docs/getting-started.md`, `docs/data-types.md` (per-bar field reference for all 17 endpoints), `docs/auth.md`, `docs/troubleshooting.md`, and five `docs/recipes/*.md` cookbooks.
- **`llms.txt`** site map per [llmstxt.org](https://llmstxt.org/).

[Unreleased]: https://github.com/FlashAlpha-lab/flashalpha-quantconnect/compare/v0.1.3...HEAD
[0.1.3]: https://github.com/FlashAlpha-lab/flashalpha-quantconnect/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/FlashAlpha-lab/flashalpha-quantconnect/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/FlashAlpha-lab/flashalpha-quantconnect/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/FlashAlpha-lab/flashalpha-quantconnect/releases/tag/v0.1.0
