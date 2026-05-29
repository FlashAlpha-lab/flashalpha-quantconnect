using System;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Narrative (verbal/LLM-friendly dealer-positioning summary) bar from the
/// FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="NarrativeResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/exposure/narrative/{symbol}?at=...</c>) one-for-one. The
/// <see cref="Narrative"/> block carries hand-tuned, numbers-aware prose lines
/// (regime, gex change, key levels, flow, vanna, charm, 0DTE, outlook) plus a
/// <see cref="NarrativeBlock.Data"/> sub-block with the raw numbers backing
/// the sentences.</para>
///
/// <para>Plan-canonical endpoint slug is <c>narrative</c>; SDK REST path is
/// <c>exposure/narrative</c>. Both resolve to the same SDK method.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaNarrativeBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaNarrative"/>.</para>
/// </remarks>
public class FlashAlphaNarrativeBar : BaseData
{
    /// <summary>Ticker echoed by the API. Symbol on <see cref="BaseData"/> is the LEAN identity.</summary>
    [JsonPropertyName("symbol")]
    public string? Ticker { get; set; }

    /// <summary>Underlying spot price at <see cref="AsOf"/>.</summary>
    [JsonPropertyName("underlying_price")]
    public double? UnderlyingPrice { get; set; }

    /// <summary>UTC timestamp the API actually used — snapped to the available minute.</summary>
    [JsonPropertyName("as_of")]
    public string? AsOf { get; set; }

    /// <summary>The narrative payload — prose lines plus the raw <c>data</c> block.</summary>
    [JsonPropertyName("narrative")]
    public NarrativeBlock? Narrative { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("narrative", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaNarrativeBar>(line, config.Symbol, date) ?? new FlashAlphaNarrativeBar();
}
