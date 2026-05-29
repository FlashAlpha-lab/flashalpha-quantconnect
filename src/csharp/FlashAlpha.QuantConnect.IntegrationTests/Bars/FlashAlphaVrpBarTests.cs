using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaVrpBar"/> — Layer 1 + Layer 2 + sugar.
/// Covers the silent-null traps documented on <see cref="FlashAlphaVrpBar"/>:
/// ZScore/Percentile live on <see cref="FlashAlpha.Historical.Models.VrpCore"/>,
/// NetGex lives on <see cref="FlashAlpha.Historical.Models.VrpRegime"/>.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaVrpBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaVrpBar));

    [Fact]
    public void FlashAlphaVrpBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("vrp", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaVrpBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.NotNull(bar.Vrp);
        Assert.NotNull(bar.Directional);
        Assert.NotNull(bar.Regime);
        Assert.NotNull(bar.Warnings);
    }

    [Fact]
    public async Task FlashAlphaVrpBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.VrpAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.VrpResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaVrpBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("vrp", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaVrpBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.MarketOpen, bar.MarketOpen);
        Assert.Equal(raw.VarianceRiskPremium, bar.VarianceRiskPremium);
        Assert.Equal(raw.ConvexityPremium, bar.ConvexityPremium);
        Assert.Equal(raw.FairVol, bar.FairVol);
        Assert.Equal(raw.NetHarvestScore, bar.NetHarvestScore);
        Assert.Equal(raw.DealerFlowRisk, bar.DealerFlowRisk);
        // Nested blocks — silent-null traps documented on the bar.
        Assert.Equal(raw.Vrp?.ZScore, bar.Vrp?.ZScore);
        Assert.Equal(raw.Vrp?.Vrp20d, bar.Vrp?.Vrp20d);
        Assert.Equal(raw.Vrp?.Percentile, bar.Vrp?.Percentile);
        Assert.Equal(raw.Directional?.DownsideVrp, bar.Directional?.DownsideVrp);
        Assert.Equal(raw.Regime?.NetGex, bar.Regime?.NetGex);
        Assert.Equal(raw.Regime?.VrpRegimeLabel, bar.Regime?.VrpRegimeLabel);
        Assert.Equal(raw.GexConditioned?.HarvestScore, bar.GexConditioned?.HarvestScore);
        Assert.Equal(raw.VannaConditioned?.Outlook, bar.VannaConditioned?.Outlook);
        Assert.Equal(raw.Macro?.Vix, bar.Macro?.Vix);
        Assert.Equal(raw.TermVrp?.Count ?? 0, bar.TermVrp?.Count ?? 0);
        Assert.Equal(raw.Warnings?.Count ?? 0, bar.Warnings?.Count ?? 0);
    }

    [Fact]
    public void AddFlashAlphaVrp_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaVrp(t); };
        Assert.NotNull(ext);
    }
}
