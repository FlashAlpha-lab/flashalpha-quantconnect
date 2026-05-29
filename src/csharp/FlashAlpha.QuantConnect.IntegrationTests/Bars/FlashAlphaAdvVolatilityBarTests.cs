using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaAdvVolatilityBar"/> — Layer 1 + Layer 2 + sugar.
/// Covers the per-expiry SVI parameter list, forward prices, the 2-D variance
/// surface (<c>double[][]</c>), arbitrage flags, variance swap fair values, and
/// the second-/third-order greek surfaces.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaAdvVolatilityBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaAdvVolatilityBar));

    [Fact]
    public void FlashAlphaAdvVolatilityBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("adv-volatility", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaAdvVolatilityBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.NotNull(bar.SviParameters);
        Assert.NotNull(bar.ForwardPrices);
        Assert.NotNull(bar.TotalVarianceSurface);
        Assert.NotNull(bar.GreeksSurfaces);
    }

    [Fact]
    public async Task FlashAlphaAdvVolatilityBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.AdvVolatilityAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.AdvVolatilityResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaAdvVolatilityBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("adv-volatility", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaAdvVolatilityBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.MarketOpen, bar.MarketOpen);
        Assert.Equal(raw.SviParameters?.Count ?? 0, bar.SviParameters?.Count ?? 0);
        Assert.Equal(raw.ForwardPrices?.Count ?? 0, bar.ForwardPrices?.Count ?? 0);
        Assert.Equal(raw.ArbitrageFlags?.Count ?? 0, bar.ArbitrageFlags?.Count ?? 0);
        Assert.Equal(raw.VarianceSwapFairValues?.Count ?? 0, bar.VarianceSwapFairValues?.Count ?? 0);
        // Total-variance surface — array lengths only; values are doubles.
        Assert.Equal(raw.TotalVarianceSurface?.Moneyness?.Length ?? 0, bar.TotalVarianceSurface?.Moneyness?.Length ?? 0);
        Assert.Equal(raw.TotalVarianceSurface?.Expiries?.Length ?? 0, bar.TotalVarianceSurface?.Expiries?.Length ?? 0);
        Assert.Equal(raw.TotalVarianceSurface?.TotalVariance?.Length ?? 0, bar.TotalVarianceSurface?.TotalVariance?.Length ?? 0);
        // Greek surfaces are nested grids; verify at least Vanna grid shape lines up.
        Assert.Equal(raw.GreeksSurfaces?.Vanna?.Strikes?.Length ?? 0, bar.GreeksSurfaces?.Vanna?.Strikes?.Length ?? 0);
    }

    [Fact]
    public void AddFlashAlphaAdvVolatility_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaAdvVolatility(t); };
        Assert.NotNull(ext);
    }
}
