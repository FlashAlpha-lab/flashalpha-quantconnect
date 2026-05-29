using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

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
