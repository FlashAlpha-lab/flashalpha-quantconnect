using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Volatility analytics bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="VolatilityResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/volatility/{symbol}?at=...</c>) one-for-one. Rich nested blocks
/// (realized vol ladder, IV-RV spread block, skew profiles per expiry, term
/// structure, IV dispersion, GEX/theta by DTE, put/call profile, OI
/// concentration, hedging scenarios, liquidity) are exposed as SDK types
/// directly so schema drift is caught at compile time.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaVolatilityBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaVolatility"/>.</para>
/// </remarks>
public class FlashAlphaVolatilityBar : BaseData
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

    /// <summary>Realized vol ladder: 5d / 10d / 20d / 30d / 60d (annualised %).</summary>
    [JsonPropertyName("realized_vol")]
    public VolatilityRealizedVol? RealizedVol { get; set; }

    /// <summary>Top-level scalar — at-the-money implied volatility (annualised %).</summary>
    [JsonPropertyName("atm_iv")]
    public double? AtmIv { get; set; }

    /// <summary>IV-RV spread block — variance risk premium across 5d / 10d / 20d / 30d horizons.</summary>
    [JsonPropertyName("iv_rv_spreads")]
    public VolatilityIvRvSpreads? IvRvSpreads { get; set; }

    /// <summary>Per-expiry skew profile (10Δ / 25Δ wings, ATM, smile ratio, tail convexity).</summary>
    [JsonPropertyName("skew_profiles")]
    public List<VolatilitySkewProfile>? SkewProfiles { get; set; }

    /// <summary>Term-structure summary (near vs far slope, contango/backwardation state).</summary>
    [JsonPropertyName("term_structure")]
    public VolatilityTermStructure? TermStructure { get; set; }

    /// <summary>IV dispersion across expiries and strikes.</summary>
    [JsonPropertyName("iv_dispersion")]
    public VolatilityIvDispersion? IvDispersion { get; set; }

    /// <summary>Net dealer gamma exposure aggregated by DTE bucket.</summary>
    [JsonPropertyName("gex_by_dte")]
    public List<VolatilityGexByDte>? GexByDte { get; set; }

    /// <summary>Net option theta aggregated by DTE bucket.</summary>
    [JsonPropertyName("theta_by_dte")]
    public List<VolatilityThetaByDte>? ThetaByDte { get; set; }

    /// <summary>Put/call profile — by-expiry OI/volume ratios + by-moneyness OI breakdown.</summary>
    [JsonPropertyName("put_call_profile")]
    public VolatilityPutCallProfile? PutCallProfile { get; set; }

    /// <summary>Open interest concentration (top-3/5/10% share + Herfindahl index).</summary>
    [JsonPropertyName("oi_concentration")]
    public VolatilityOiConcentration? OiConcentration { get; set; }

    /// <summary>±X% hedging scenarios — projected dealer share rebalance and notional.</summary>
    [JsonPropertyName("hedging_scenarios")]
    public List<VolatilityHedgingScenario>? HedgingScenarios { get; set; }

    /// <summary>Bid-ask liquidity at the ATM and wing regions.</summary>
    [JsonPropertyName("liquidity")]
    public VolatilityLiquidity? Liquidity { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("volatility", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaVolatilityBar>(line, config.Symbol, date) ?? new FlashAlphaVolatilityBar();
}

/// <summary>
/// Advanced volatility analytics bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="AdvVolatilityResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/adv_volatility/{symbol}?at=...</c>) one-for-one. Carries per-expiry
/// SVI parameters, forward prices, the full total-variance surface, arbitrage
/// flags, variance swap fair values, and second-/third-order greek surfaces.</para>
///
/// <para>Cold-cache responses can take ~1.5s — bars should be requested with
/// generous timeouts. Plan-canonical endpoint slug is <c>adv-volatility</c>
/// (hyphenated); SDK REST path is <c>adv_volatility</c> (underscored). Both
/// resolve to the same SDK method.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaAdvVolatilityBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaAdvVolatility"/>.</para>
/// </remarks>
public class FlashAlphaAdvVolatilityBar : BaseData
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

    /// <summary>Per-expiry SVI parameter set: (a, b, ρ, m, σ) plus forward and ATM-total-variance.</summary>
    [JsonPropertyName("svi_parameters")]
    public List<AdvVolatilitySviParams>? SviParameters { get; set; }

    /// <summary>Per-expiry forward prices and basis vs spot.</summary>
    [JsonPropertyName("forward_prices")]
    public List<AdvVolatilityForwardPrice>? ForwardPrices { get; set; }

    /// <summary>Total variance surface — log-moneyness × tenor grid plus implied-vol grid.</summary>
    [JsonPropertyName("total_variance_surface")]
    public AdvVolatilityVarianceSurface? TotalVarianceSurface { get; set; }

    /// <summary>Detected butterfly / calendar arbitrage violations across the surface.</summary>
    [JsonPropertyName("arbitrage_flags")]
    public List<AdvVolatilityArbitrageFlag>? ArbitrageFlags { get; set; }

    /// <summary>Variance swap fair values per expiry, with convexity adjustment.</summary>
    [JsonPropertyName("variance_swap_fair_values")]
    public List<AdvVolatilityVarianceSwap>? VarianceSwapFairValues { get; set; }

    /// <summary>Second-/third-order greek surfaces (vanna, charm, volga, speed).</summary>
    [JsonPropertyName("greeks_surfaces")]
    public AdvVolatilityGreeksSurfaces? GreeksSurfaces { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("adv-volatility", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaAdvVolatilityBar>(line, config.Symbol, date) ?? new FlashAlphaAdvVolatilityBar();
}
