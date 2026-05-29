using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaChexBar"/> — Layer 1 + Layer 2 + sugar.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaChexBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaChexBar));

    [Fact]
    public void FlashAlphaChexBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/chex", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaChexBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.True(bar.NetChex.HasValue, "NetChex should populate");
        Assert.NotNull(bar.Strikes);
    }

    [Fact]
    public async Task FlashAlphaChexBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.ChexAsync("SPY", atString, expiration: null);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.ChexResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaChexBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/chex", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaChexBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.NetChex, bar.NetChex);
        Assert.Equal(raw.ChexInterpretation, bar.ChexInterpretation);
        Assert.NotNull(bar.Strikes);
        Assert.Equal(raw.Strikes?.Count ?? 0, bar.Strikes!.Count);
    }

    [Fact]
    public void AddFlashAlphaChex_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaChex(t); };
        Assert.NotNull(ext);
    }
}
