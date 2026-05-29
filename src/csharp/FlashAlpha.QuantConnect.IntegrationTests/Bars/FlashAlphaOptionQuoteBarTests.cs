using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using NodaTime;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaOptionQuoteBar"/> — Layer 1 + Layer 2 + sugar.
/// The bar is the only one in the catalog whose upstream JSON root is an
/// ARRAY (one row per contract), so the bar overrides <c>Reader</c> to
/// bypass the object-only <c>FlashAlphaJsonMapper</c>.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaOptionQuoteBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaOptionQuoteBar));

    [Fact]
    public void FlashAlphaOptionQuoteBar_FetchAndParse_PopulatesQuotes()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("option/quote", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        // The bar's overridden Reader is what we exercise here — Parse<T> via the
        // generic mapper would no-op against an array root.
        var config = new global::QuantConnect.Data.SubscriptionDataConfig(
            typeof(FlashAlphaOptionQuoteBar),
            TestSymbol,
            global::QuantConnect.Resolution.Daily,
            DateTimeZone.Utc,
            DateTimeZone.Utc,
            fillForward: false,
            extendedHours: false,
            isInternalFeed: false,
            isCustom: true);
        var prototype = new FlashAlphaOptionQuoteBar();
        var bar = (FlashAlphaOptionQuoteBar)prototype.Reader(config, source.Source, TestDate, isLiveMode: false);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.NotNull(bar.Quotes);
        Assert.True(bar.Quotes!.Count > 10, "Option chain should populate multiple rows");
        var first = bar.Quotes[0];
        Assert.True(first.Strike.HasValue, "Per-row strike should populate");
        Assert.True(first.Type == "C" || first.Type == "P", $"Type should be 'C' or 'P' — got '{first.Type}'");
    }

    [Fact]
    public async Task FlashAlphaOptionQuoteBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var raw = await sdk.OptionQuoteAsync("SPY", atString, expiry: null, strike: null, type: null);

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaOptionQuoteBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("option/quote", symbol, TestDate);

        var config = new global::QuantConnect.Data.SubscriptionDataConfig(
            typeof(FlashAlphaOptionQuoteBar),
            symbol,
            global::QuantConnect.Resolution.Daily,
            DateTimeZone.Utc,
            DateTimeZone.Utc,
            fillForward: false,
            extendedHours: false,
            isInternalFeed: false,
            isCustom: true);
        var prototype = new FlashAlphaOptionQuoteBar();
        var bar = (FlashAlphaOptionQuoteBar)prototype.Reader(config, source.Source, TestDate, isLiveMode: false);

        Assert.NotNull(bar);
        Assert.Equal(JsonValueKind.Array, raw.ValueKind);
        Assert.Equal(raw.GetArrayLength(), bar.Quotes?.Count ?? 0);

        // Compare the first row salient fields.
        var rawFirst = raw[0];
        var barFirst = bar.Quotes![0];
        Assert.Equal(rawFirst.GetProperty("strike").GetDouble(), barFirst.Strike);
        Assert.Equal(rawFirst.GetProperty("type").GetString(), barFirst.Type);
        if (rawFirst.TryGetProperty("expiry", out var rawExpiry))
        {
            Assert.Equal(rawExpiry.GetString(), barFirst.Expiry);
        }
        if (rawFirst.TryGetProperty("delta", out var rawDelta) && rawDelta.ValueKind == JsonValueKind.Number)
        {
            Assert.Equal(rawDelta.GetDouble(), barFirst.Delta);
        }
    }

    [Fact]
    public void AddFlashAlphaOptionQuote_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaOptionQuote(t); };
        Assert.NotNull(ext);
    }
}
