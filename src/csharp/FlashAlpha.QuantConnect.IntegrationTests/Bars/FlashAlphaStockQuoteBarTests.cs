using System;
using System.Text.Json;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaStockQuoteBar"/> — Layer 1 + Layer 2 + sugar.
/// The historical SDK has no typed <c>StockQuoteResponse</c> model, so Layer 2
/// compares against the raw <see cref="JsonElement"/> field-by-field. Notable
/// wire quirk: the root key is <c>ticker</c> (not <c>symbol</c>) and the
/// timestamp field is camelCase <c>lastUpdate</c>.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaStockQuoteBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaStockQuoteBar));

    [Fact]
    public void FlashAlphaStockQuoteBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("stock/quote", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaStockQuoteBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.Equal("SPY", bar.Ticker);
        Assert.True(bar.Bid.HasValue && bar.Bid > 0, "Bid should populate");
        Assert.True(bar.Ask.HasValue && bar.Ask > 0, "Ask should populate");
        Assert.True(bar.Mid.HasValue && bar.Mid > 0, "Mid should populate");
        Assert.True(bar.Bid <= bar.Mid && bar.Mid <= bar.Ask, "Bid <= Mid <= Ask");
        Assert.False(string.IsNullOrEmpty(bar.LastUpdate), "LastUpdate should populate");
    }

    [Fact]
    public async Task FlashAlphaStockQuoteBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var raw = await sdk.StockQuoteAsync("SPY", atString);

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaStockQuoteBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("stock/quote", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaStockQuoteBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.GetProperty("ticker").GetString(), bar!.Ticker);
        Assert.Equal(raw.GetProperty("bid").GetDouble(), bar.Bid);
        Assert.Equal(raw.GetProperty("ask").GetDouble(), bar.Ask);
        Assert.Equal(raw.GetProperty("mid").GetDouble(), bar.Mid);
        Assert.Equal(raw.GetProperty("lastUpdate").GetString(), bar.LastUpdate);
    }

    [Fact]
    public void AddFlashAlphaStockQuote_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaStockQuote(t); };
        Assert.NotNull(ext);
    }
}
