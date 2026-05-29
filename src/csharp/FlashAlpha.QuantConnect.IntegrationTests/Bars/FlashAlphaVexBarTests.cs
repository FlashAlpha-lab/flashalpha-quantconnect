using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaVexBar"/> — Layer 1 + Layer 2 + sugar.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaVexBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaVexBar));

    [Fact]
    public void FlashAlphaVexBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/vex", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaVexBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.True(bar.NetVex.HasValue, "NetVex should populate");
        Assert.NotNull(bar.Strikes);
    }

    [Fact]
    public async Task FlashAlphaVexBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.VexAsync("SPY", atString, expiration: null);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.VexResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaVexBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/vex", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaVexBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.NetVex, bar.NetVex);
        Assert.Equal(raw.VexInterpretation, bar.VexInterpretation);
        Assert.NotNull(bar.Strikes);
        Assert.Equal(raw.Strikes?.Count ?? 0, bar.Strikes!.Count);
    }

    [Fact]
    public void AddFlashAlphaVex_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaVex(t); };
        Assert.NotNull(ext);
    }
}
