using System;
using System.Collections.Concurrent;
using System.Text.Json;
using FlashAlpha.QuantConnect.Client;
using QuantConnect;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Shared <c>GetSource</c> / <c>Reader</c> plumbing for every FlashAlpha bar.
/// </summary>
/// <remarks>
/// Implements the Owned-HTTP branch from the auth-mechanism ADR
/// (<c>docs/superpowers/specs/2026-05-28-auth-mechanism-decision.md</c>):
/// fetches JSON via <see cref="FlashAlphaHttpClient"/> in <see cref="For"/>,
/// caches per <c>(endpoint, ticker, date)</c>, and returns a sentinel URL
/// that <see cref="Parse{T}"/> uses to look up the cached JSON.
/// </remarks>
public static class FlashAlphaSource
{
    private const string SentinelPrefix = "flashalpha://";

    private static readonly Lazy<FlashAlphaHttpClient> _http =
        new(() => new FlashAlphaHttpClient());

    private static readonly ConcurrentDictionary<string, string> _cache = new();

    /// <summary>
    /// Eagerly fetches the JSON for <paramref name="endpoint"/> /
    /// <paramref name="symbol"/> / <paramref name="date"/>, stores it in the
    /// per-key cache, and returns a sentinel <see cref="SubscriptionDataSource"/>
    /// whose URL the bar's <c>Reader</c> hands back to <see cref="Parse{T}"/>.
    /// </summary>
    /// <param name="endpoint">FlashAlpha endpoint slug (e.g. <c>"exposure/gex"</c>).</param>
    /// <param name="symbol">LEAN symbol — its <c>Value</c> is used as the ticker.</param>
    /// <param name="date">The bar date.</param>
    /// <returns>A <see cref="SubscriptionDataSource"/> carrying the sentinel URL.</returns>
    public static SubscriptionDataSource For(string endpoint, Symbol symbol, DateTime date)
    {
        var key = MakeKey(endpoint, symbol.Value, date);
        // Eager fetch — sync-over-async because LEAN's GetSource is sync.
        //
        // Sparse data is the expected case for LEAN custom-data subscriptions —
        // weekends, holidays, pre-RTH midnight ticks on Daily resolution, etc.
        // When the FlashAlpha API reports no data at the requested timestamp
        // (NoDataException / NoCoverageException / InvalidAtException), write
        // an empty file so Parse returns null and LEAN skips the bar cleanly.
        //
        // Date shift: LEAN daily-res ticks midnight UTC but FlashAlpha has
        // market-hours data only. Shift midnight to 20:00 UTC (16:00 ET, NYSE
        // close) so the API returns the session's closing snapshot.
        var apiAt = ShiftToMarketHours(date);
        //
        // Transport: LocalFile — LEAN's RestSubscriptionStreamReader would try
        // to HTTP-fetch a URL, so a custom in-memory sentinel scheme isn't
        // workable. Writing JSON as a single-line file under the OS tempdir
        // and using LocalFile + FileFormat.Csv lets LEAN's standard line-by-line
        // reader hand the full JSON to Reader as the `line` argument in one call.
        string payload;
        try
        {
            payload = _http.Value
                .FetchJsonAsync(endpoint, symbol.Value, apiAt)
                .GetAwaiter().GetResult();
        }
        catch (FlashAlpha.Historical.NoDataException)
        {
            payload = string.Empty;
        }
        catch (FlashAlpha.Historical.NoCoverageException)
        {
            payload = string.Empty;
        }
        catch (FlashAlpha.Historical.InvalidAtException)
        {
            payload = string.Empty;
        }

        _cache[key] = payload;

        // Persist as a single-line file under the OS tempdir.
        var tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "flashalpha-quantconnect");
        System.IO.Directory.CreateDirectory(tmpDir);
        var safeKey = key.Replace("/", "_").Replace("|", "_");
        var path = System.IO.Path.Combine(tmpDir, $"{safeKey}.json");
        System.IO.File.WriteAllText(path, payload);

        return new SubscriptionDataSource(
            path,
            SubscriptionTransportMedium.LocalFile,
            FileFormat.Csv);
    }

    /// <summary>
    /// LEAN daily-res ticks at midnight UTC but FlashAlpha has market-hours
    /// data only. Shift any midnight-UTC date to 20:00 UTC (16:00 ET, NYSE
    /// close) so the API returns the session's closing snapshot. Non-midnight
    /// dates pass through unchanged.
    /// </summary>
    private static DateTime ShiftToMarketHours(DateTime date)
    {
        if (date.Hour == 0 && date.Minute == 0 && date.Second == 0)
        {
            return date.AddHours(20);
        }
        return date;
    }

    /// <summary>
    /// Parses <paramref name="line"/> (either the sentinel URL or raw JSON)
    /// into a new bar of type <typeparamref name="T"/>, populating its
    /// PascalCase properties from snake_case JSON keys via
    /// <see cref="FlashAlphaJsonMapper"/>.
    /// </summary>
    /// <typeparam name="T">The bar type — must be a parameterless-constructible
    /// <see cref="BaseData"/>.</typeparam>
    /// <param name="line">Either the sentinel URL produced by <see cref="For"/>
    /// or, as a fallback, the raw JSON itself.</param>
    /// <param name="symbol">The LEAN symbol to assign to the bar.</param>
    /// <param name="date">The bar's <c>Time</c>; <c>EndTime</c> is
    /// <paramref name="date"/> + 1 day (daily default — sub-daily bars may override).</param>
    /// <returns>The populated bar, or <c>null</c> if no JSON is resolvable.</returns>
    public static T? Parse<T>(string line, Symbol symbol, DateTime date) where T : BaseData, new()
    {
        // line is either the sentinel ("flashalpha://...") or, if LEAN's
        // downloader bypassed our cache for some reason, the raw JSON.
        var json = ResolveJson(line);
        if (string.IsNullOrWhiteSpace(json)) return null;

        var bar = new T
        {
            Symbol = symbol,
            Time = date,
            EndTime = date.AddDays(1),
        };

        using var doc = JsonDocument.Parse(json);
        FlashAlphaJsonMapper.PopulateProperties(bar, doc.RootElement);
        return bar;
    }

    private static string ResolveJson(string line)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;
        if (!line.StartsWith(SentinelPrefix, StringComparison.Ordinal)) return line;

        var key = line.Substring(SentinelPrefix.Length);
        return _cache.TryGetValue(key, out var cached) ? cached : string.Empty;
    }

    /// <summary>
    /// Resolves the sentinel URL (or raw JSON fallback) to the cached JSON
    /// payload. Public hook for bars whose JSON root is not an object
    /// (e.g. <see cref="FlashAlphaOptionQuoteBar"/> whose payload is an array)
    /// and therefore can't use <see cref="Parse{T}"/>'s
    /// <see cref="FlashAlphaJsonMapper"/> pipeline.
    /// </summary>
    /// <param name="line">Sentinel URL or raw JSON.</param>
    /// <returns>The cached JSON string, or empty when unresolved.</returns>
    public static string ResolveJsonForBar(string line) => ResolveJson(line);

    private static string MakeKey(string endpoint, string ticker, DateTime date)
        => $"{endpoint}|{ticker}|{date:yyyy-MM-dd}";

    /// <summary>Reset cache — for tests only.</summary>
    public static void Reset()
    {
        _cache.Clear();
    }
}
