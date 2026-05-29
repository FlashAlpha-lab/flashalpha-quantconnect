using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Stock-summary composite bar from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="StockSummaryResponse"/> from the FlashAlpha.Historical SDK
/// (<c>GET /v1/stock/{symbol}/summary?at=...</c>) one-for-one. This is the
/// "single best snapshot" endpoint — price, volatility (ATM IV, HV20/60, VRP,
/// 25-delta skew, IV term structure), aggregated options flow, the full
/// dealer-exposure block (greeks, walls, gamma flip, max pain, hedging
/// estimates, 0DTE, top strikes), and macro context (VIX/VVIX/SKEW/SPX/MOVE).</para>
///
/// <para><b>Historical-specific gaps</b> (verbatim from the SDK docstring):
/// <list type="bullet">
///   <item><see cref="OptionsFlow"/> volume fields and <c>pc_ratio_volume</c>
///     are always <c>0</c> / <c>null</c> — no minute volume on replay.</item>
///   <item><see cref="StockSummaryMacro.VixFutures"/> is always <c>null</c>
///     — CME futures are not historically reconstructible from minute data.</item>
///   <item><see cref="StockSummaryMacro.FearAndGreed"/> is always <c>null</c>
///     — the CNN index is not archived.</item>
/// </list></para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaStockSummaryBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaStockSummary"/>.</para>
/// </remarks>
public class FlashAlphaStockSummaryBar : BaseData
{
    /// <summary>Ticker echoed by the API. Symbol on <see cref="BaseData"/> is the LEAN identity.</summary>
    [JsonPropertyName("symbol")]
    public string? Ticker { get; set; }

    /// <summary>UTC timestamp the API actually used — snapped to the available minute.</summary>
    [JsonPropertyName("as_of")]
    public string? AsOf { get; set; }

    /// <summary><c>true</c> when the bar was resolved during the US-equity session.</summary>
    [JsonPropertyName("market_open")]
    public bool? MarketOpen { get; set; }

    /// <summary>Top-of-book price block — bid / ask / mid / last + last-update timestamp.
    /// Renamed from the SDK's <c>price</c> field to avoid colliding with LEAN
    /// <see cref="BaseData.Price"/> (the underlying scalar used for plotting).</summary>
    [JsonPropertyName("price")]
    public StockSummaryPrice? PriceQuote { get; set; }

    /// <summary>Volatility block — ATM IV, HV20/60, VRP, 25-delta skew, IV term structure.</summary>
    [JsonPropertyName("volatility")]
    public StockSummaryVolatility? Volatility { get; set; }

    /// <summary>Aggregate options-flow stats — OI/volume by side + put/call ratios.</summary>
    [JsonPropertyName("options_flow")]
    public StockSummaryOptionsFlow? OptionsFlow { get; set; }

    /// <summary>Dealer-exposure block — greeks, walls, gamma flip, max pain,
    /// hedging estimates, 0DTE breakdown, top strikes.</summary>
    [JsonPropertyName("exposure")]
    public StockSummaryExposure? Exposure { get; set; }

    /// <summary>Macro context — VIX/VVIX/SKEW/SPX/MOVE + term structure + fear-and-greed.</summary>
    [JsonPropertyName("macro")]
    public StockSummaryMacro? Macro { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("stock/summary", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaStockSummaryBar>(line, config.Symbol, date) ?? new FlashAlphaStockSummaryBar();
}
