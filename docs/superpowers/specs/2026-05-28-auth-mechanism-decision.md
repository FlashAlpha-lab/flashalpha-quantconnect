# Auth mechanism decision

**Date:** 2026-05-28
**Status:** Accepted
**Author:** Implementer subagent during Task 3 of `2026-05-28-flashalpha-quantconnect.md`

## Decision

The bridge uses the **Owned-HTTP** auth mechanism. `FlashAlphaHttpClient`
(wrapping `FlashAlpha.Historical` / `flashalpha_historical`) fetches JSON for
the requested `(endpoint, ticker, at)` triple via the SDK's own HTTP layer,
caches the payload in process memory, and `FlashAlphaSource.Reader(...)` reads
from that cache via a sentinel `SubscriptionDataSource` URL.

## Evidence

Two `curl` probes against `https://historical.flashalpha.com/v1/exposure/gex/SPY?at=2024-06-14` on 2026-05-28:

| Auth scheme | HTTP status | Time | Body |
|---|---|---|---|
| `?api_key=…` query parameter | **401** | 0.13 s | empty |
| `Authorization: Bearer …` header | **401** | 0.37 s | empty |
| `X-Api-Key: …` header | **200** | 0.23 s | 69,647 bytes of valid GEX JSON |

The 401s are immediate and definitive — the server is reachable and processing
requests in sub-second time. Only `X-Api-Key` is honored.

Cross-checked against the canonical client used by every other flashalpha-* SDK:
`flashalpha-historical-python/src/flashalpha_historical/client.py:115` sets
`self._session.headers["X-Api-Key"] = api_key`. The dotnet SDK ships the same
scheme. The bridge therefore inherits the right behavior simply by delegating
HTTP to those SDKs — no custom auth code on our side.

## Implication for FlashAlphaSource

QC LEAN's `SubscriptionTransportMedium.RemoteFile` downloader does not pass
custom HTTP headers, so the bridge cannot rely on LEAN's downloader to set
`X-Api-Key`. Query-param fallback is not available either (the server returns
401 for it).

Both `src/csharp/FlashAlpha.QuantConnect/Data/FlashAlphaSource.cs` and
`src/python/src/flashalpha_quantconnect/data/source.py` must therefore use
the **Owned-HTTP** code branch from Task 8 / Task 9 of the plan:

1. `GetSource(endpoint, symbol, date)` calls `FlashAlphaHttpClient.FetchJsonAsync(...)`
   eagerly, stashes the JSON in a process-static `ConcurrentDictionary<string,string>`
   (C#) or `dict[str,str]` + lock (Python) keyed by `"{endpoint}|{ticker}|{yyyy-MM-dd}"`,
   and returns a sentinel `SubscriptionDataSource` of the form
   `flashalpha://{key}` with `SubscriptionTransportMedium.Rest`.
2. `Reader(config, line, date, isLiveMode)` checks for the `flashalpha://`
   prefix, parses the key, and reads the cached JSON from the dictionary.
   If the prefix is absent (LEAN passed an actual line), it treats `line`
   as the JSON body directly.

The Query-Param code paths shown in the plan's Task 8/9 are **not implemented**.

## Lifetime of the in-process cache

The cache is reset implicitly at process boundary (LEAN restart). No TTL or
eviction inside the bridge — backtest runs are short enough that the cache
holding ~one entry per `(endpoint, ticker, trading day)` is bounded by the
backtest period. For a year-long backtest of one ticker subscribing to four
endpoints at daily resolution: ~252 × 4 = ~1k entries × ~70 KB each = ~70 MB
peak. Acceptable.

## Open follow-ups

- If the API later adds query-param auth or a long-lived signed-URL scheme,
  revisit — that would let us use LEAN's native `RemoteFile` downloader and
  drop the in-process cache. Not planned in v0.x.
- The 70 KB / entry estimate is from a single live probe (GEX SPY 2024-06-14).
  Some endpoints (`surface`, `exposure/levels`) may be larger; revisit memory
  bounds if a real backtest reports concerning RSS.

## Source

Spike performed against `historical.flashalpha.com` on 2026-05-28 with a
live API key, then immediately deleted from the local filesystem. Credential
files used during the spike: `E:/tmp/fa_qp.cfg` and `E:/tmp/fa_hdr.cfg`,
both shredded after use.
