using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Volatility-surface bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="SurfaceResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/surface/{symbol}?at=...</c>) one-for-one — field types and
/// nullability match, so any schema drift is caught at compile time when the
/// SDK bumps.</para>
///
/// <para>Subscribe in a LEAN algorithm with:
/// <code>algo.AddData&lt;FlashAlphaSurfaceBar&gt;("SPY", Resolution.Daily);</code>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaSurface"/>.</para>
///
/// <para>The IV grid is exposed as the SDK's <c>double[][]</c> — one row per
/// tenor in <see cref="Tenors"/>, one column per moneyness bucket in
/// <see cref="Moneyness"/>.</para>
/// </remarks>
public class FlashAlphaSurfaceBar : BaseData
{
    /// <summary>Ticker echoed by the API. Symbol on <see cref="BaseData"/> is the LEAN identity.</summary>
    [JsonPropertyName("symbol")]
    public string? Ticker { get; set; }

    /// <summary>Underlying spot price at <see cref="AsOf"/>.</summary>
    [JsonPropertyName("spot")]
    public double? Spot { get; set; }

    /// <summary>Server-side timestamp the row was resolved at (ISO-8601 string).</summary>
    [JsonPropertyName("as_of")]
    public string? AsOf { get; set; }

    /// <summary>Side length of the IV grid — both <see cref="Tenors"/> and
    /// <see cref="Moneyness"/> have this many entries.</summary>
    [JsonPropertyName("grid_size")]
    public int? GridSize { get; set; }

    /// <summary>Tenors (years) defining the rows of <see cref="Iv"/>.</summary>
    [JsonPropertyName("tenors")]
    public double[]? Tenors { get; set; }

    /// <summary>Moneyness levels defining the columns of <see cref="Iv"/>.</summary>
    [JsonPropertyName("moneyness")]
    public double[]? Moneyness { get; set; }

    /// <summary>Implied vol grid — <c>Iv[tenorIndex][moneynessIndex]</c>.</summary>
    [JsonPropertyName("iv")]
    public double[][]? Iv { get; set; }

    /// <summary>Expirations that contributed to the smoothed surface.</summary>
    [JsonPropertyName("slices_used")]
    public List<string>? SlicesUsed { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("surface", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaSurfaceBar>(line, config.Symbol, date) ?? new FlashAlphaSurfaceBar();
}
