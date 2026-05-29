using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaMaxPainBar"/> — Layer 1 + Layer 2 + sugar.
/// Covers the nested-DTO fall-through (Distance, DealerAlignment, ExpectedMove)
/// plus the per-strike pain curve / OI lists.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaMaxPainBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaMaxPainBar));

    [Fact]
    public void FlashAlphaMaxPainBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("max-pain", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaMaxPainBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.True(bar.MaxPainStrike.HasValue, "MaxPainStrike should populate");
        Assert.NotNull(bar.Distance);
        Assert.NotNull(bar.PainCurve);
    }

    [Fact]
    public async Task FlashAlphaMaxPainBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.MaxPainAsync("SPY", atString, expiration: null);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.MaxPainResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaMaxPainBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("max-pain", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaMaxPainBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.MaxPainStrike, bar.MaxPainStrike);
        Assert.Equal(raw.Signal, bar.Signal);
        Assert.Equal(raw.Expiration, bar.Expiration);
        Assert.Equal(raw.PutCallOiRatio, bar.PutCallOiRatio);
        Assert.Equal(raw.Regime, bar.Regime);
        Assert.Equal(raw.PinProbability, bar.PinProbability);
        Assert.Equal(raw.Distance?.Percent, bar.Distance?.Percent);
        Assert.Equal(raw.DealerAlignment?.Alignment, bar.DealerAlignment?.Alignment);
        Assert.Equal(raw.ExpectedMove?.StraddlePrice, bar.ExpectedMove?.StraddlePrice);
        Assert.Equal(raw.PainCurve?.Count ?? 0, bar.PainCurve?.Count ?? 0);
        Assert.Equal(raw.OiByStrike?.Count ?? 0, bar.OiByStrike?.Count ?? 0);
        Assert.Equal(raw.MaxPainByExpiration?.Count ?? 0, bar.MaxPainByExpiration?.Count ?? 0);
    }

    [Fact]
    public void AddFlashAlphaMaxPain_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaMaxPain(t); };
        Assert.NotNull(ext);
    }
}
