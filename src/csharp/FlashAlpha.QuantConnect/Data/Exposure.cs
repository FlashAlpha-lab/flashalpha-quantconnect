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

/// <summary>
/// Delta exposure (DEX) bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="DexResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/exposure/dex/{symbol}?at=...</c>) one-for-one.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaDexBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaDex"/>.</para>
/// </remarks>
public class FlashAlphaDexBar : BaseData
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

    /// <summary>Net dealer delta exposure in dollars.</summary>
    [JsonPropertyName("net_dex")]
    public double? NetDex { get; set; }

    /// <summary>Per-strike DEX table. See <see cref="DexStrikeRow"/>.</summary>
    [JsonPropertyName("strikes")]
    public List<DexStrikeRow>? Strikes { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/dex", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaDexBar>(line, config.Symbol, date) ?? new FlashAlphaDexBar();
}

/// <summary>
/// Vanna exposure (VEX) bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="VexResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/exposure/vex/{symbol}?at=...</c>) one-for-one. Includes
/// a textual <see cref="VexInterpretation"/> describing the directional
/// vol-spot linkage.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaVexBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaVex"/>.</para>
/// </remarks>
public class FlashAlphaVexBar : BaseData
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

    /// <summary>Headline net dealer vanna exposure.</summary>
    [JsonPropertyName("net_vex")]
    public double? NetVex { get; set; }

    /// <summary>Plain-English explanation of the vanna regime. Safe to surface verbatim.</summary>
    [JsonPropertyName("vex_interpretation")]
    public string? VexInterpretation { get; set; }

    /// <summary>Per-strike VEX table. See <see cref="VexStrikeRow"/>.</summary>
    [JsonPropertyName("strikes")]
    public List<VexStrikeRow>? Strikes { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/vex", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaVexBar>(line, config.Symbol, date) ?? new FlashAlphaVexBar();
}

/// <summary>
/// Charm exposure (CHEX) bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="ChexResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/exposure/chex/{symbol}?at=...</c>) one-for-one. Includes a
/// textual <see cref="ChexInterpretation"/> describing the charm regime.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaChexBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaChex"/>.</para>
/// </remarks>
public class FlashAlphaChexBar : BaseData
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

    /// <summary>Headline net dealer charm exposure.</summary>
    [JsonPropertyName("net_chex")]
    public double? NetChex { get; set; }

    /// <summary>Plain-English explanation of the charm regime. Safe to surface verbatim.</summary>
    [JsonPropertyName("chex_interpretation")]
    public string? ChexInterpretation { get; set; }

    /// <summary>Per-strike CHEX table. See <see cref="ChexStrikeRow"/>.</summary>
    [JsonPropertyName("strikes")]
    public List<ChexStrikeRow>? Strikes { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/chex", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaChexBar>(line, config.Symbol, date) ?? new FlashAlphaChexBar();
}

/// <summary>
/// Exposure summary bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="ExposureSummaryResponse"/> from the
/// FlashAlpha.Historical SDK (<c>GET /v1/exposure/summary/{symbol}?at=...</c>)
/// one-for-one. The nested blocks (<see cref="Exposures"/>,
/// <see cref="Interpretation"/>, <see cref="HedgingEstimate"/>,
/// <see cref="ZeroDte"/>) are exposed as the SDK types directly so any
/// schema drift inside them is caught at compile time.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaExposureSummaryBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaExposureSummary"/>.</para>
/// </remarks>
public class FlashAlphaExposureSummaryBar : BaseData
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

    /// <summary>One of <c>"positive_gamma"</c>, <c>"negative_gamma"</c>, or <c>"unknown"</c>.</summary>
    [JsonPropertyName("regime")]
    public string? Regime { get; set; }

    /// <summary>Net exposure totals across the entire chain.</summary>
    [JsonPropertyName("exposures")]
    public ExposureSummaryExposures? Exposures { get; set; }

    /// <summary>Verbal interpretation of the gamma/vanna/charm regimes.</summary>
    [JsonPropertyName("interpretation")]
    public ExposureSummaryInterpretation? Interpretation { get; set; }

    /// <summary>Estimated dealer hedging flow at +/- 1% spot moves.</summary>
    [JsonPropertyName("hedging_estimate")]
    public ExposureSummaryHedgingEstimate? HedgingEstimate { get; set; }

    /// <summary>Same-day-expiration contribution to total GEX.</summary>
    [JsonPropertyName("zero_dte")]
    public ExposureSummaryZeroDte? ZeroDte { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/summary", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaExposureSummaryBar>(line, config.Symbol, date)
            ?? new FlashAlphaExposureSummaryBar();
}

/// <summary>
/// Exposure levels bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="ExposureLevelsResponse"/> from the
/// FlashAlpha.Historical SDK (<c>GET /v1/exposure/levels/{symbol}?at=...</c>)
/// one-for-one. The nested <see cref="Levels"/> block is exposed as the
/// SDK <see cref="ExposureLevels"/> type directly so schema drift is caught
/// at compile time.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaExposureLevelsBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaExposureLevels"/>.</para>
/// </remarks>
public class FlashAlphaExposureLevelsBar : BaseData
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

    /// <summary>The distilled set of dealer-flow key levels (gamma flip, walls, etc.).</summary>
    [JsonPropertyName("levels")]
    public ExposureLevels? Levels { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/levels", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaExposureLevelsBar>(line, config.Symbol, date)
            ?? new FlashAlphaExposureLevelsBar();
}
