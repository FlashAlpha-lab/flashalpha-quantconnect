using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaExposureLevelsBar"/> —
/// Layer 1 + Layer 2 + sugar. Verifies the nested <c>Levels</c> block round-trips
/// through the JSON mapper.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaExposureLevelsBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaExposureLevelsBar));

    [Fact]
    public void FlashAlphaExposureLevelsBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/levels", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaExposureLevelsBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.NotNull(bar.Levels);
    }

    [Fact]
    public async Task FlashAlphaExposureLevelsBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.ExposureLevelsAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.ExposureLevelsResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaExposureLevelsBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/levels", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaExposureLevelsBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.Levels?.GammaFlip, bar.Levels?.GammaFlip);
        Assert.Equal(raw.Levels?.MaxPositiveGamma, bar.Levels?.MaxPositiveGamma);
        Assert.Equal(raw.Levels?.MaxNegativeGamma, bar.Levels?.MaxNegativeGamma);
        Assert.Equal(raw.Levels?.CallWall, bar.Levels?.CallWall);
        Assert.Equal(raw.Levels?.PutWall, bar.Levels?.PutWall);
        Assert.Equal(raw.Levels?.HighestOiStrike, bar.Levels?.HighestOiStrike);
        Assert.Equal(raw.Levels?.ZeroDteMagnet, bar.Levels?.ZeroDteMagnet);
    }

    [Fact]
    public void AddFlashAlphaExposureLevels_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaExposureLevels(t); };
        Assert.NotNull(ext);
    }
}
