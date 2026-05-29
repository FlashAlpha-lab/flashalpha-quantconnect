using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuantConnect.Data;

namespace FlashAlpha.QuantConnect.Data;

/// <summary>
/// Option-quote bar from the FlashAlpha historical API — one row per option
/// contract in the chain at the requested minute.
/// </summary>
/// <remarks>
/// <para>Mirrors the JSON response from <c>GET /v1/optionquote/{symbol}?at=...</c>.
/// With <c>expiry</c> + <c>strike</c> + <c>type</c> all set the upstream
/// endpoint returns a single object; bar subscriptions only carry
/// <c>(ticker, date)</c> — i.e. the bar always requests the unfiltered form
/// and the API returns a JSON array. The bar exposes that array verbatim as
/// <see cref="Quotes"/>.</para>
///
/// <para><b>Historical-specific gaps</b> (from the SDK docstring):
/// <list type="bullet">
///   <item>Per-row <see cref="OptionQuoteRow.BidSize"/> / <see cref="OptionQuoteRow.AskSize"/>
///     are always <c>0</c> — minute table has no sizes.</item>
///   <item>Per-row <see cref="OptionQuoteRow.Volume"/> is always <c>0</c>.</item>
///   <item>Per-row <see cref="OptionQuoteRow.SviVol"/> is always <c>null</c>
///     with <see cref="OptionQuoteRow.SviVolGated"/> set to <c>"backtest_mode"</c>.</item>
/// </list></para>
///
/// <para><b>Reader override:</b> the upstream JSON root is an array, not an
/// object, so this bar bypasses <see cref="FlashAlphaJsonMapper"/> and
/// deserialises the array directly into <see cref="Quotes"/>.</para>
///
/// <para>Subscribe with: <c>algo.AddData&lt;FlashAlphaOptionQuoteBar&gt;("SPY", Resolution.Daily);</c>
/// or the sugar <see cref="QCAlgorithmExtensions.AddFlashAlphaOptionQuote"/>.</para>
/// </remarks>
public class FlashAlphaOptionQuoteBar : BaseData
{
    /// <summary>The full option-chain array — one <see cref="OptionQuoteRow"/> per contract.</summary>
    public List<OptionQuoteRow>? Quotes { get; set; }

    /// <inheritdoc cref="FlashAlphaGexBar.GetSource"/>
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        => FlashAlphaSource.For("option/quote", config.Symbol, date);

    /// <summary>
    /// Resolves the sentinel URL to the cached JSON array and deserialises it
    /// into <see cref="Quotes"/>. Falls back to an empty bar on a cache miss
    /// or malformed payload.
    /// </summary>
    public override BaseData Reader(
        SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
    {
        var bar = new FlashAlphaOptionQuoteBar
        {
            Symbol = config.Symbol,
            Time = date,
            EndTime = date.AddDays(1),
        };

        var json = FlashAlphaSource.ResolveJsonForBar(line);
        if (string.IsNullOrWhiteSpace(json)) return bar;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                bar.Quotes = JsonSerializer.Deserialize<List<OptionQuoteRow>>(
                    root.GetRawText(),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    });
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return the empty bar so LEAN sees a non-null
            // BaseData and skips this minute rather than dying.
        }

        return bar;
    }
}

/// <summary>
/// One row of <see cref="FlashAlphaOptionQuoteBar.Quotes"/> — a single option
/// contract's bid/ask/mid + first- and second-order greeks + OI.
/// </summary>
/// <remarks>
/// Wire-format note: a few fields are camelCase upstream
/// (<c>bidSize</c>, <c>askSize</c>, <c>lastUpdate</c>) — preserved with
/// explicit <see cref="JsonPropertyNameAttribute"/>.
/// </remarks>
public sealed class OptionQuoteRow
{
    /// <summary><c>"C"</c> or <c>"P"</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Expiration date (<c>yyyy-MM-dd</c>).</summary>
    [JsonPropertyName("expiry")]
    public string? Expiry { get; set; }

    [JsonPropertyName("strike")]
    public double? Strike { get; set; }

    [JsonPropertyName("bid")]
    public double? Bid { get; set; }

    [JsonPropertyName("ask")]
    public double? Ask { get; set; }

    /// <summary>Mid = (bid + ask) / 2.</summary>
    [JsonPropertyName("mid")]
    public double? Mid { get; set; }

    /// <summary>Always <c>0</c> on historical replay — minute table has no sizes.</summary>
    [JsonPropertyName("bidSize")]
    public int? BidSize { get; set; }

    /// <summary>Always <c>0</c> on historical replay — minute table has no sizes.</summary>
    [JsonPropertyName("askSize")]
    public int? AskSize { get; set; }

    /// <summary>Last quote update timestamp. Wire field is camelCase.</summary>
    [JsonPropertyName("lastUpdate")]
    public string? LastUpdate { get; set; }

    /// <summary>Underlying mid price at the quote time.</summary>
    [JsonPropertyName("underlying")]
    public double? Underlying { get; set; }

    /// <summary>Implied vol from the mid price (annualised %).</summary>
    [JsonPropertyName("implied_vol")]
    public double? ImpliedVol { get; set; }

    /// <summary>IV inverted from the bid price.</summary>
    [JsonPropertyName("iv_bid")]
    public double? IvBid { get; set; }

    /// <summary>IV inverted from the ask price.</summary>
    [JsonPropertyName("iv_ask")]
    public double? IvAsk { get; set; }

    [JsonPropertyName("delta")]
    public double? Delta { get; set; }

    [JsonPropertyName("gamma")]
    public double? Gamma { get; set; }

    [JsonPropertyName("theta")]
    public double? Theta { get; set; }

    [JsonPropertyName("vega")]
    public double? Vega { get; set; }

    [JsonPropertyName("rho")]
    public double? Rho { get; set; }

    /// <summary>∂²V/∂S∂σ — sensitivity of delta to vol changes.</summary>
    [JsonPropertyName("vanna")]
    public double? Vanna { get; set; }

    /// <summary>∂²V/∂S∂t — sensitivity of delta to time decay.</summary>
    [JsonPropertyName("charm")]
    public double? Charm { get; set; }

    /// <summary>Always <c>null</c> on historical replay (<c>svi_vol_gated="backtest_mode"</c>).</summary>
    [JsonPropertyName("svi_vol")]
    public double? SviVol { get; set; }

    /// <summary>Always <c>"backtest_mode"</c> on historical replay.</summary>
    [JsonPropertyName("svi_vol_gated")]
    public string? SviVolGated { get; set; }

    [JsonPropertyName("open_interest")]
    public int? OpenInterest { get; set; }

    /// <summary>Always <c>0</c> on historical replay — no minute volume.</summary>
    [JsonPropertyName("volume")]
    public int? Volume { get; set; }
}
