using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Variance risk premium (VRP) bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="VrpResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/vrp/{symbol}?at=...</c>) one-for-one. Nested blocks
/// (<see cref="Vrp"/>, <see cref="Directional"/>, <see cref="GexConditioned"/>,
/// <see cref="VannaConditioned"/>, <see cref="Regime"/>,
/// <see cref="StrategyScores"/>, <see cref="Macro"/>) and the term-VRP list are
/// exposed as the SDK types directly so schema drift is caught at compile time.</para>
///
/// <para>Common silent-null traps: <c>ZScore</c> / <c>Percentile</c> live on
/// <see cref="Vrp"/>; <c>NetGex</c> lives on <see cref="Regime"/>;
/// <c>HarvestScore</c> on the top level refers to
/// <c>GexConditioned.HarvestScore</c>, while <see cref="NetHarvestScore"/> is a
/// separate composite. On historical responses with short warm-up,
/// <see cref="StrategyScores"/> and <see cref="NetHarvestScore"/> can be
/// <c>null</c> — check <see cref="Warnings"/>.</para>
///
/// <para>Returns 403 <c>tier_restricted</c> for anything below Alpha plan.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaVrpBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaVrp"/>.</para>
/// </remarks>
public class FlashAlphaVrpBar : BaseData
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

    /// <summary><c>true</c> when the bar was resolved during the US-equity session.</summary>
    [JsonPropertyName("market_open")]
    public bool? MarketOpen { get; set; }

    /// <summary>Core VRP metrics block. ZScore/Percentile live here, NOT on the top level.</summary>
    [JsonPropertyName("vrp")]
    public VrpCore? Vrp { get; set; }

    /// <summary>Headline VRP scalar.</summary>
    [JsonPropertyName("variance_risk_premium")]
    public double? VarianceRiskPremium { get; set; }

    /// <summary>Convexity premium.</summary>
    [JsonPropertyName("convexity_premium")]
    public double? ConvexityPremium { get; set; }

    /// <summary>Fair vol estimate.</summary>
    [JsonPropertyName("fair_vol")]
    public double? FairVol { get; set; }

    /// <summary>Directional VRP skew (downside/upside wings).</summary>
    [JsonPropertyName("directional")]
    public VrpDirectional? Directional { get; set; }

    /// <summary>VRP term structure — one row per DTE bucket.</summary>
    [JsonPropertyName("term_vrp")]
    public List<VrpTermItem>? TermVrp { get; set; }

    /// <summary>VRP harvest score conditioned on the dealer-gamma regime.</summary>
    [JsonPropertyName("gex_conditioned")]
    public VrpGexConditioned? GexConditioned { get; set; }

    /// <summary>VRP outlook conditioned on net dealer vanna exposure.</summary>
    [JsonPropertyName("vanna_conditioned")]
    public VrpVannaConditioned? VannaConditioned { get; set; }

    /// <summary>Regime snapshot block. NetGex lives HERE, not on the top level.</summary>
    [JsonPropertyName("regime")]
    public VrpRegime? Regime { get; set; }

    /// <summary>0-100 strategy suitability scores. Null on early historical timestamps.</summary>
    [JsonPropertyName("strategy_scores")]
    public VrpStrategyScores? StrategyScores { get; set; }

    /// <summary>0-100 composite harvest signal. Null on early historical timestamps.</summary>
    [JsonPropertyName("net_harvest_score")]
    public int? NetHarvestScore { get; set; }

    /// <summary>Dealer-flow risk score.</summary>
    [JsonPropertyName("dealer_flow_risk")]
    public int? DealerFlowRisk { get; set; }

    /// <summary>Server-side warnings about data quality. Always present (possibly empty).</summary>
    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }

    /// <summary>Macro-context snapshot used to condition the VRP outlook.</summary>
    [JsonPropertyName("macro")]
    public VrpMacro? Macro { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("vrp", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaVrpBar>(line, config.Symbol, date) ?? new FlashAlphaVrpBar();
}
