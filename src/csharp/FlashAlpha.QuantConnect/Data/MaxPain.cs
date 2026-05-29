using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Max-pain bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="MaxPainResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/max-pain/{symbol}?at=...</c>) one-for-one. Nested objects
/// (<see cref="Distance"/>, <see cref="DealerAlignment"/>,
/// <see cref="ExpectedMove"/>) and per-strike lists
/// (<see cref="PainCurve"/>, <see cref="OiByStrike"/>,
/// <see cref="MaxPainByExpiration"/>) are exposed as the SDK types directly so
/// schema drift is caught at compile time.</para>
///
/// <para>When the request resolves to a single expiry,
/// <see cref="MaxPainByExpiration"/> is <c>null</c> and <see cref="Expiration"/>
/// is populated. When the request rolls up all expiries, <see cref="Expiration"/>
/// is <c>null</c> and <see cref="MaxPainByExpiration"/> carries one row per
/// expiry.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaMaxPainBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaMaxPain"/>.</para>
/// </remarks>
public class FlashAlphaMaxPainBar : BaseData
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

    /// <summary>Strike at which total option-writer pain is minimized.</summary>
    [JsonPropertyName("max_pain_strike")]
    public double? MaxPainStrike { get; set; }

    /// <summary>Distance from spot to <see cref="MaxPainStrike"/> (absolute, percent, direction).</summary>
    [JsonPropertyName("distance")]
    public MaxPainDistance? Distance { get; set; }

    /// <summary>Coarse pin/magnet signal (e.g. <c>"pin"</c>, <c>"gravitate"</c>).</summary>
    [JsonPropertyName("signal")]
    public string? Signal { get; set; }

    /// <summary>Single-expiry mode: the expiration this row was computed for
    /// (YYYY-MM-DD). <c>null</c> when <see cref="MaxPainByExpiration"/> is populated.</summary>
    [JsonPropertyName("expiration")]
    public string? Expiration { get; set; }

    /// <summary>Put OI divided by call OI across the request's expiry scope.</summary>
    [JsonPropertyName("put_call_oi_ratio")]
    public double? PutCallOiRatio { get; set; }

    /// <summary>Strike-by-strike pain curve. Minimum is at <see cref="MaxPainStrike"/>.</summary>
    [JsonPropertyName("pain_curve")]
    public List<MaxPainCurveRow>? PainCurve { get; set; }

    /// <summary>Per-strike OI + volume breakdown. Same strike grid as <see cref="PainCurve"/>.</summary>
    [JsonPropertyName("oi_by_strike")]
    public List<MaxPainOiRow>? OiByStrike { get; set; }

    /// <summary>Roll-up-all-expiries mode: one row per expiry. <c>null</c> when
    /// <see cref="Expiration"/> is populated.</summary>
    [JsonPropertyName("max_pain_by_expiration")]
    public List<MaxPainByExpirationRow>? MaxPainByExpiration { get; set; }

    /// <summary>Whether dealer-flow walls (gamma flip, call wall, put wall)
    /// align with the pain strike.</summary>
    [JsonPropertyName("dealer_alignment")]
    public MaxPainDealerAlignment? DealerAlignment { get; set; }

    /// <summary>Coarse regime label (e.g. <c>"calm"</c>, <c>"trending"</c>).</summary>
    [JsonPropertyName("regime")]
    public string? Regime { get; set; }

    /// <summary>Expected move (straddle + ATM IV) and whether pain falls inside it.</summary>
    [JsonPropertyName("expected_move")]
    public MaxPainExpectedMove? ExpectedMove { get; set; }

    /// <summary>Composite 0-100 score for the likelihood spot pins to <see cref="MaxPainStrike"/>.</summary>
    [JsonPropertyName("pin_probability")]
    public int? PinProbability { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("max-pain", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaMaxPainBar>(line, config.Symbol, date) ?? new FlashAlphaMaxPainBar();
}
