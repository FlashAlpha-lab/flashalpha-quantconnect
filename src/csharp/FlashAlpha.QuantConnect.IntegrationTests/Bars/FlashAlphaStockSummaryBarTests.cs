using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaStockSummaryBar"/> — Layer 1 + Layer 2 + sugar.
/// Covers the rich nested composite (PriceQuote, Volatility, OptionsFlow,
/// Exposure, Macro) round-tripping through the JSON mapper.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaStockSummaryBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaStockSummaryBar));

    [Fact]
    public void FlashAlphaStockSummaryBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("stock/summary", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaStockSummaryBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.NotNull(bar.PriceQuote);
        Assert.NotNull(bar.Volatility);
        Assert.NotNull(bar.OptionsFlow);
        Assert.NotNull(bar.Macro);
    }

    [Fact]
    public async Task FlashAlphaStockSummaryBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.StockSummaryAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.StockSummaryResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaStockSummaryBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("stock/summary", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaStockSummaryBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.MarketOpen, bar.MarketOpen);
        // Nested blocks — compare salient leaves only.
        Assert.Equal(raw.Price?.Mid, bar.PriceQuote?.Mid);
        Assert.Equal(raw.Price?.LastUpdate, bar.PriceQuote?.LastUpdate);
        Assert.Equal(raw.Volatility?.AtmIv, bar.Volatility?.AtmIv);
        Assert.Equal(raw.Volatility?.Vrp, bar.Volatility?.Vrp);
        Assert.Equal(raw.OptionsFlow?.PcRatioOi, bar.OptionsFlow?.PcRatioOi);
        Assert.Equal(raw.Exposure?.NetGex, bar.Exposure?.NetGex);
        Assert.Equal(raw.Exposure?.GammaFlip, bar.Exposure?.GammaFlip);
        Assert.Equal(raw.Exposure?.Regime, bar.Exposure?.Regime);
        Assert.Equal(raw.Macro?.Vix?.Value, bar.Macro?.Vix?.Value);
        Assert.Equal(
            raw.Exposure?.TopStrikes?.Count ?? 0,
            bar.Exposure?.TopStrikes?.Count ?? 0);
    }

    [Fact]
    public void AddFlashAlphaStockSummary_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaStockSummary(t); };
        Assert.NotNull(ext);
    }
}
