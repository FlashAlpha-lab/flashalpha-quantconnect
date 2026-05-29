using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Zero-DTE (same-day-expiry) dealer-positioning bar from the FlashAlpha
/// historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="ZeroDteResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/exposure/zero-dte/{symbol}?at=...</c>) one-for-one. The rich
/// nested blocks (regime, exposures, expected move, pin risk, hedging, decay,
/// vol context, flow, levels, liquidity, metadata) are exposed as the SDK
/// types directly so any schema drift inside them is caught at compile time.</para>
///
/// <para>On names without a same-day expiry the SDK returns a "thin" response —
/// <see cref="NoZeroDte"/> = <c>true</c>, <see cref="Message"/> populated, all
/// nested blocks <c>null</c>, and <see cref="NextZeroDteExpiry"/> pointing at
/// the next available expiry. Subscribers must null-check.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaZeroDteBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaZeroDte"/>.</para>
/// </remarks>
public class FlashAlphaZeroDteBar : BaseData
{
    /// <summary>Ticker echoed by the API. Symbol on <see cref="BaseData"/> is the LEAN identity.</summary>
    [JsonPropertyName("symbol")]
    public string? Ticker { get; set; }

    /// <summary>Underlying spot price at <see cref="AsOf"/>.</summary>
    [JsonPropertyName("underlying_price")]
    public double? UnderlyingPrice { get; set; }

    /// <summary>Same-day expiration date (YYYY-MM-DD).</summary>
    [JsonPropertyName("expiration")]
    public string? Expiration { get; set; }

    /// <summary>Server-side timestamp the row was resolved at (ISO-8601 string).</summary>
    [JsonPropertyName("as_of")]
    public string? AsOf { get; set; }

    /// <summary><c>true</c> when the bar was resolved during the US-equity session.</summary>
    [JsonPropertyName("market_open")]
    public bool? MarketOpen { get; set; }

    /// <summary>Hours remaining until the cash session close.</summary>
    [JsonPropertyName("time_to_close_hours")]
    public double? TimeToCloseHours { get; set; }

    /// <summary>Fraction of the session still ahead — 1.0 at open, 0.0 at close.</summary>
    [JsonPropertyName("time_to_close_pct")]
    public double? TimeToClosePct { get; set; }

    /// <summary>Regime label + gamma-flip context.</summary>
    [JsonPropertyName("regime")]
    public ZeroDteRegime? Regime { get; set; }

    /// <summary>0DTE net Greek exposures plus share-of-total-chain GEX.</summary>
    [JsonPropertyName("exposures")]
    public ZeroDteExposures? Exposures { get; set; }

    /// <summary>Implied 1-sigma move (full session and remaining), plus ATM IV.</summary>
    [JsonPropertyName("expected_move")]
    public ZeroDteExpectedMove? ExpectedMove { get; set; }

    /// <summary>Magnet strike, pin score, and component breakdown.</summary>
    [JsonPropertyName("pin_risk")]
    public ZeroDtePinRisk? PinRisk { get; set; }

    /// <summary>Dealer hedging flow estimates across a grid of spot moves.</summary>
    [JsonPropertyName("hedging")]
    public ZeroDteHedging? Hedging { get; set; }

    /// <summary>Theta and charm regime for the remaining session.</summary>
    [JsonPropertyName("decay")]
    public ZeroDteDecay? Decay { get; set; }

    /// <summary>0DTE vs 7DTE ATM IV, VIX, vanna exposure.</summary>
    [JsonPropertyName("vol_context")]
    public ZeroDteVolContext? VolContext { get; set; }

    /// <summary>0DTE volume / OI breakdown and put-call ratios.</summary>
    [JsonPropertyName("flow")]
    public ZeroDteFlow? Flow { get; set; }

    /// <summary>0DTE-specific dealer-flow key levels (call/put walls, magnet, etc.).</summary>
    [JsonPropertyName("levels")]
    public ZeroDteLevels? Levels { get; set; }

    /// <summary>Spread + execution-score liquidity metrics for the 0DTE chain.</summary>
    [JsonPropertyName("liquidity")]
    public ZeroDteLiquidity? Liquidity { get; set; }

    /// <summary>Snapshot age and data-quality scores.</summary>
    [JsonPropertyName("metadata")]
    public ZeroDteMetadata? Metadata { get; set; }

    /// <summary>Per-strike 0DTE breakdown — full Greek + flow snapshot.</summary>
    [JsonPropertyName("strikes")]
    public List<ZeroDteStrike>? Strikes { get; set; }

    /// <summary>Non-fatal warnings the engine emitted for this response.</summary>
    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }

    /// <summary><c>true</c> when there is no same-day expiry — see <see cref="NextZeroDteExpiry"/>.</summary>
    [JsonPropertyName("no_zero_dte")]
    public bool? NoZeroDte { get; set; }

    /// <summary>Human-readable message accompanying a "no zero-DTE" response.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Next available expiry when <see cref="NoZeroDte"/> is <c>true</c>.</summary>
    [JsonPropertyName("next_zero_dte_expiry")]
    public string? NextZeroDteExpiry { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("exposure/zero-dte", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaZeroDteBar>(line, config.Symbol, date) ?? new FlashAlphaZeroDteBar();
}
