using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaZeroDteBar"/> — Layer 1 + Layer 2 + sugar.
/// Covers the rich nested-DTO fall-through path (Regime, Exposures, ExpectedMove,
/// PinRisk, Hedging, Decay, VolContext, Flow, Levels, Liquidity, Metadata).
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaZeroDteBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaZeroDteBar));

    [Fact]
    public void FlashAlphaZeroDteBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/zero-dte", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaZeroDteBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        // On a no-zero-DTE response, all the nested blocks may be null and
        // NoZeroDte will be true. Otherwise the rich payload populates.
        if (bar.NoZeroDte != true)
        {
            Assert.NotNull(bar.Regime);
            Assert.NotNull(bar.Exposures);
            Assert.NotNull(bar.ExpectedMove);
            Assert.NotNull(bar.Strikes);
        }
    }

    [Fact]
    public async Task FlashAlphaZeroDteBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.ZeroDteAsync("SPY", atString, strikeRange: null);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.ZeroDteResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaZeroDteBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/zero-dte", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaZeroDteBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.Expiration, bar.Expiration);
        Assert.Equal(raw.MarketOpen, bar.MarketOpen);
        Assert.Equal(raw.NoZeroDte, bar.NoZeroDte);
        // Nested blocks: compare salient scalars; deep equality is intentionally
        // out of scope (drift surfaces as a top-level mismatch).
        Assert.Equal(raw.Regime?.Label, bar.Regime?.Label);
        Assert.Equal(raw.Exposures?.NetGex, bar.Exposures?.NetGex);
        Assert.Equal(raw.ExpectedMove?.AtmIv, bar.ExpectedMove?.AtmIv);
        Assert.Equal(raw.PinRisk?.PinScore, bar.PinRisk?.PinScore);
        Assert.Equal(raw.Levels?.CallWall, bar.Levels?.CallWall);
        Assert.Equal(raw.Flow?.TotalVolume, bar.Flow?.TotalVolume);
        Assert.Equal(raw.Strikes?.Count ?? 0, bar.Strikes?.Count ?? 0);
    }

    [Fact]
    public void AddFlashAlphaZeroDte_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaZeroDte(t); };
        Assert.NotNull(ext);
    }
}
