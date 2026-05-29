using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaVolatilityBar"/> — Layer 1 + Layer 2 + sugar.
/// Covers the nested-DTO fall-through path (RealizedVol, IvRvSpreads, TermStructure,
/// IvDispersion, PutCallProfile, OiConcentration, Liquidity) plus the per-expiry /
/// per-bucket lists (SkewProfiles, GexByDte, ThetaByDte, HedgingScenarios).
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaVolatilityBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaVolatilityBar));

    [Fact]
    public void FlashAlphaVolatilityBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("volatility", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaVolatilityBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.True(bar.AtmIv.HasValue, "AtmIv should populate");
        Assert.NotNull(bar.RealizedVol);
        Assert.NotNull(bar.IvRvSpreads);
        Assert.NotNull(bar.TermStructure);
    }

    [Fact]
    public async Task FlashAlphaVolatilityBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.VolatilityAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.VolatilityResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaVolatilityBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("volatility", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaVolatilityBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.MarketOpen, bar.MarketOpen);
        Assert.Equal(raw.AtmIv, bar.AtmIv);
        // Nested blocks: compare salient scalars; deep equality intentionally out of scope.
        Assert.Equal(raw.RealizedVol?.Rv20d, bar.RealizedVol?.Rv20d);
        Assert.Equal(raw.IvRvSpreads?.Vrp20d, bar.IvRvSpreads?.Vrp20d);
        Assert.Equal(raw.TermStructure?.State, bar.TermStructure?.State);
        Assert.Equal(raw.IvDispersion?.CrossExpiry, bar.IvDispersion?.CrossExpiry);
        Assert.Equal(raw.OiConcentration?.Herfindahl, bar.OiConcentration?.Herfindahl);
        Assert.Equal(raw.Liquidity?.AtmAvgSpreadPct, bar.Liquidity?.AtmAvgSpreadPct);
        Assert.Equal(raw.SkewProfiles?.Count ?? 0, bar.SkewProfiles?.Count ?? 0);
        Assert.Equal(raw.GexByDte?.Count ?? 0, bar.GexByDte?.Count ?? 0);
        Assert.Equal(raw.ThetaByDte?.Count ?? 0, bar.ThetaByDte?.Count ?? 0);
        Assert.Equal(raw.HedgingScenarios?.Count ?? 0, bar.HedgingScenarios?.Count ?? 0);
    }

    [Fact]
    public void AddFlashAlphaVolatility_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaVolatility(t); };
        Assert.NotNull(ext);
    }
}
