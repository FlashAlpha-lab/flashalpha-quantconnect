using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using FlashAlpha.QuantConnect.Client;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Coverage / supported-tickers bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="TickersResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/tickers</c> — list form, no <c>symbol</c> filter). Each row in
/// <see cref="Tickers"/> carries a symbol plus its first/last covered session
/// and a healthy-day count.</para>
///
/// <para><b>Special case:</b> this endpoint is the only one in the bridge that
/// is NOT ticker-scoped. The bar still subscribes under a LEAN symbol
/// (whatever you pass to <c>AddData</c>) — the HTTP client ignores the ticker
/// when the endpoint slug is <c>"tickers"</c> and the SDK is called with
/// <c>symbol: null</c>. Subscribing under different LEAN symbols just gives
/// you separate cache entries containing the same global coverage table.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaTickersBar&gt;("_universe", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaTickers"/>.</para>
/// </remarks>
public class FlashAlphaTickersBar : BaseData
{
    /// <summary>Per-symbol coverage rows. Use this to drive a universe selector.</summary>
    [JsonPropertyName("tickers")]
    public List<TickersRow>? Tickers { get; set; }

    /// <summary>Length of <see cref="Tickers"/>.</summary>
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("tickers", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaTickersBar>(line, config.Symbol, date) ?? new FlashAlphaTickersBar();
}

/// <summary>
/// LEAN universe-selection model backed by FlashAlpha's <c>tickers</c> endpoint.
/// </summary>
/// <remarks>
/// <para>On each daily refresh, this model pulls the supported-tickers list
/// (with coverage metadata) from the FlashAlpha historical API and emits a
/// universe containing a US-equity <c>QuantConnect.Symbol</c> for every row
/// passing the predicate. The predicate sees the SDK's
/// <see cref="TickersRow"/> directly so callers can gate by coverage span,
/// healthy-day count, or any field on the SDK's coverage block.</para>
///
/// <para>Wire it in <c>Initialize</c>:
/// <code>
/// algo.AddUniverseSelection(new FlashAlphaTickersUniverse(
///     row =&gt; (row.Coverage?.HealthyDays ?? 0) &gt; 30));
/// </code></para>
///
/// <para><b>Why the heavy ctor with <see cref="SecurityType.Base"/>:</b> the
/// stock <c>(string name, Func selector)</c> overload of
/// <see cref="CustomUniverseSelectionModel"/> instantiates a placeholder equity
/// symbol that bootstraps LEAN's map-file provider — which is not available
/// outside the LEAN runtime. Using the heavy overload with
/// <see cref="SecurityType.Base"/> keeps construction map-file-free; the
/// emitted ticker strings still resolve to US-equity symbols at selection
/// time inside LEAN.</para>
///
/// <para>The HTTP path mirrors the bar: the <c>tickers</c> endpoint ignores the
/// ticker argument; we pass a sentinel <c>"_universe"</c> for the cache key.</para>
/// </remarks>
public sealed class FlashAlphaTickersUniverse : CustomUniverseSelectionModel
{
    private const string UniverseName = "flashalpha-tickers-universe";
    private static readonly UniverseSettings DefaultSettings =
        new(Resolution.Daily, leverage: 1m, fillForward: true,
            extendedMarketHours: false, minimumTimeInUniverse: TimeSpan.Zero);

    private readonly Func<TickersRow, bool> _filter;
    private readonly IFlashAlphaHttpClient? _client;
    private readonly bool _ownsClient;

    /// <summary>
    /// Construct a universe-selection model that pulls the FlashAlpha tickers
    /// list and emits US-equity Symbols for the rows passing <paramref name="filter"/>.
    /// </summary>
    /// <param name="filter">Row-by-row predicate; rows that return <c>true</c> are
    /// added to the universe. <c>null</c> means "include every covered ticker".</param>
    /// <param name="client">Optional HTTP client override — primarily for tests.
    /// When <c>null</c>, a process-local <see cref="FlashAlphaHttpClient"/> is
    /// instantiated per <see cref="SelectTickers"/> call and disposed.</param>
    public FlashAlphaTickersUniverse(
        Func<TickersRow, bool>? filter = null,
        IFlashAlphaHttpClient? client = null)
        : base(SecurityType.Base, UniverseName, Market.USA,
               _ => Enumerable.Empty<string>(),
               DefaultSettings, TimeSpan.FromDays(1))
    {
        _filter = filter ?? (_ => true);
        _client = client;
        _ownsClient = client is null;
    }

    /// <summary>
    /// Algorithm-aware selector — LEAN calls this once per refresh interval.
    /// Defers to <see cref="SelectTickers"/> for the HTTP + filter work.
    /// </summary>
    public override IEnumerable<string> Select(QCAlgorithm algorithm, DateTime date)
        => SelectTickers(date);

    /// <summary>
    /// Algorithm-free overload of the selector — does the HTTP fetch + filter
    /// without needing a constructed <see cref="QCAlgorithm"/>. Useful for
    /// unit tests and direct introspection of the selected universe.
    /// </summary>
    /// <param name="date">As-of date the universe is being computed for.</param>
    /// <returns>Surviving ticker strings (e.g. <c>"SPY"</c>, <c>"QQQ"</c>).</returns>
    public IEnumerable<string> SelectTickers(DateTime date)
    {
        var http = _client ?? new FlashAlphaHttpClient();
        string raw;
        try
        {
            // Tickers endpoint ignores the ticker arg — pass the same sentinel
            // the bar subscription uses so the per-key cache lines up.
            raw = http.FetchJsonAsync("tickers", "_universe", date)
                .GetAwaiter().GetResult();
        }
        finally
        {
            if (_ownsClient) (http as IDisposable)?.Dispose();
        }

        if (string.IsNullOrWhiteSpace(raw)) yield break;

        TickersResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TickersResponse>(raw);
        }
        catch (JsonException)
        {
            yield break;
        }

        if (parsed?.Tickers is null) yield break;

        foreach (var row in parsed.Tickers)
        {
            if (row is null || string.IsNullOrEmpty(row.Symbol)) continue;
            if (!_filter(row)) continue;
            yield return row.Symbol;
        }
    }
}
