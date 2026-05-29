using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaNarrativeBar"/> — Layer 1 + Layer 2 + sugar.
/// Verifies the verbal-payload nested block (regime/key-levels/flow/outlook
/// prose lines plus the raw <c>data</c> sub-block) round-trips through the
/// JSON mapper unchanged.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaNarrativeBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaNarrativeBar));

    [Fact]
    public void FlashAlphaNarrativeBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("narrative", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaNarrativeBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.NotNull(bar.Narrative);
        Assert.NotNull(bar.Narrative!.Data);
    }

    [Fact]
    public async Task FlashAlphaNarrativeBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.NarrativeAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.NarrativeResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaNarrativeBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("narrative", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaNarrativeBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        // Narrative block — compare prose lines + a salient datum.
        Assert.Equal(raw.Narrative?.Regime, bar.Narrative?.Regime);
        Assert.Equal(raw.Narrative?.GexChange, bar.Narrative?.GexChange);
        Assert.Equal(raw.Narrative?.KeyLevels, bar.Narrative?.KeyLevels);
        Assert.Equal(raw.Narrative?.Outlook, bar.Narrative?.Outlook);
        Assert.Equal(raw.Narrative?.Data?.NetGex, bar.Narrative?.Data?.NetGex);
        Assert.Equal(raw.Narrative?.Data?.GammaFlip, bar.Narrative?.Data?.GammaFlip);
        Assert.Equal(raw.Narrative?.Data?.CallWall, bar.Narrative?.Data?.CallWall);
        Assert.Equal(raw.Narrative?.Data?.PutWall, bar.Narrative?.Data?.PutWall);
        Assert.Equal(raw.Narrative?.Data?.Regime, bar.Narrative?.Data?.Regime);
        Assert.Equal(
            raw.Narrative?.Data?.TopOiChanges?.Count ?? 0,
            bar.Narrative?.Data?.TopOiChanges?.Count ?? 0);
    }

    [Fact]
    public void AddFlashAlphaNarrative_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaNarrative(t); };
        Assert.NotNull(ext);
    }
}
