using System;
using System.Text.Json.Serialization;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Stock-quote bar (bid/ask/mid/last) from the FlashAlpha historical API.
/// </summary>
/// <remarks>
/// <para>Mirrors the JSON response from <c>GET /v1/stockquote/{symbol}?at=...</c>.
/// The historical SDK's <c>StockQuoteAsync</c> returns a raw
/// <see cref="System.Text.Json.JsonElement"/> (no typed model in the SDK), so
/// the bar carries each documented field explicitly.</para>
///
/// <para><b>Wire-format note:</b> the upstream feed uses camelCase for
/// <c>lastUpdate</c> (NOT <c>last_update</c>) — preserved here with an explicit
/// <see cref="JsonPropertyNameAttribute"/>.</para>
///
/// <para>The bar root key is <c>ticker</c> (not <c>symbol</c>) on this endpoint
/// — see <see cref="Ticker"/>'s attribute.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaStockQuoteBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaStockQuote"/>.</para>
/// </remarks>
public class FlashAlphaStockQuoteBar : BaseData
{
    /// <summary>Ticker echoed by the API. Symbol on <see cref="BaseData"/> is the LEAN identity.</summary>
    [JsonPropertyName("ticker")]
    public string? Ticker { get; set; }

    /// <summary>Best bid for the underlier at <see cref="LastUpdate"/>.</summary>
    [JsonPropertyName("bid")]
    public double? Bid { get; set; }

    /// <summary>Best ask for the underlier at <see cref="LastUpdate"/>.</summary>
    [JsonPropertyName("ask")]
    public double? Ask { get; set; }

    /// <summary>Mid price: <c>(bid + ask) / 2</c>.</summary>
    [JsonPropertyName("mid")]
    public double? Mid { get; set; }

    /// <summary>Last trade price for the underlier.</summary>
    [JsonPropertyName("last")]
    public double? Last { get; set; }

    /// <summary>Last quote/trade update timestamp. Wire field is camelCase.</summary>
    [JsonPropertyName("lastUpdate")]
    public string? LastUpdate { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("stock/quote", config.Symbol, date);

    /// <inheritdoc cref="FlashAlphaGexBar.Reader"/>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        => FlashAlphaSource.Parse<FlashAlphaStockQuoteBar>(line, config.Symbol, date) ?? new FlashAlphaStockQuoteBar();
}
