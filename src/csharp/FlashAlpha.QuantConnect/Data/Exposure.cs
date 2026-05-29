using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Gamma exposure (GEX) bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="GexResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/exposure/gex/{symbol}?at=...</c>) one-for-one — field types and
/// nullability match, so any schema drift is caught at compile time when the
/// SDK bumps.</para>
///
/// <para>Subscribe in a LEAN algorithm with:
/// <code>algo.AddData&lt;FlashAlphaGexBar&gt;("SPY", Resolution.Daily);</code>
/// </para>
///
/// <para>The bar plumbs through <see cref="FlashAlphaSource"/> — <see cref="GetSource"/>
/// eagerly fetches the JSON and returns a cache sentinel, and <see cref="Reader"/>
/// resolves that sentinel back to a populated bar.</para>
/// </remarks>
public class FlashAlphaGexBar : BaseData
{
    /// <summary>Ticker echoed by the API. Symbol on <see cref="BaseData"/> is the LEAN identity.</summary>
    [JsonPropertyName("symbol")]
    public string? Ticker { get; set; }

    /// <summary>Underlying spot price at <see cref="AsOf"/>.</summary>
    [JsonPropertyName("underlying_price")]
    public double? UnderlyingPrice { get; set; }

    /// <summary>Server-side timestamp the row was resolved at (ISO-8601 string).</summary>
    [JsonPropertyName("as_of")]
    public string? AsOf { get; set; }

    /// <summary>Strike where net dealer gamma crosses zero. <c>null</c> when no zero crossing exists.</summary>
    [JsonPropertyName("gamma_flip")]
    public double? GammaFlip { get; set; }

    /// <summary>Headline net dealer gamma exposure in dollars per 1% spot move.</summary>
    [JsonPropertyName("net_gex")]
    public double? NetGex { get; set; }

    /// <summary>Coarse label — typically <c>"positive"</c> / <c>"negative"</c>.</summary>
    [JsonPropertyName("net_gex_label")]
    public string? NetGexLabel { get; set; }

    /// <summary>Per-strike GEX table. See <see cref="GexStrikeRow"/>.</summary>
    [JsonPropertyName("strikes")]
    public List<GexStrikeRow>? Strikes { get; set; }

    /// <summary>
    /// Returns a sentinel <see cref="SubscriptionDataSource"/> backed by the
    /// in-process <see cref="FlashAlphaSource"/> cache.
    /// </summary>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/gex", config.Symbol, date);

    /// <summary>
    /// Resolves the sentinel URL (or raw JSON, as a fallback) handed in by LEAN
    /// and returns a populated bar. Returns an empty bar — never <c>null</c> —
    /// so LEAN's non-nullable <c>BaseData.Reader</c> contract holds.
    /// </summary>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaGexBar>(line, config.Symbol, date) ?? new FlashAlphaGexBar();
}
