using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaSurfaceBar"/> — Layer 1 + Layer 2 + sugar.
/// Exercises the 2-D <c>double[][]</c> IV grid round-trip through the JSON mapper.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaSurfaceBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaSurfaceBar));

    [Fact]
    public void FlashAlphaSurfaceBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("surface", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaSurfaceBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.Spot > 0, "Spot should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.True(bar.GridSize.HasValue && bar.GridSize > 0, "GridSize should populate");
        Assert.NotNull(bar.Tenors);
        Assert.NotNull(bar.Moneyness);
        Assert.NotNull(bar.Iv);
    }

    [Fact]
    public async Task FlashAlphaSurfaceBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.SurfaceAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.SurfaceResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaSurfaceBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("surface", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaSurfaceBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.Spot, bar.Spot);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.GridSize, bar.GridSize);
        Assert.Equal(raw.Tenors?.Length ?? 0, bar.Tenors?.Length ?? 0);
        Assert.Equal(raw.Moneyness?.Length ?? 0, bar.Moneyness?.Length ?? 0);
        Assert.Equal(raw.Iv?.Length ?? 0, bar.Iv?.Length ?? 0);
        Assert.Equal(raw.SlicesUsed?.Count ?? 0, bar.SlicesUsed?.Count ?? 0);
    }

    [Fact]
    public void AddFlashAlphaSurface_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaSurface(t); };
        Assert.NotNull(ext);
    }
}
