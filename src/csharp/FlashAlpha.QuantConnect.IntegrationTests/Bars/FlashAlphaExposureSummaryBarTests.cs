using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaExposureSummaryBar"/> —
/// Layer 1 + Layer 2 + sugar. Covers the nested-DTO fall-through path
/// (Exposures, Interpretation, HedgingEstimate, ZeroDte).
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaExposureSummaryBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaExposureSummaryBar));

    [Fact]
    public void FlashAlphaExposureSummaryBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/summary", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaExposureSummaryBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.False(string.IsNullOrEmpty(bar.Regime), "Regime should populate");
        Assert.NotNull(bar.Exposures);
        Assert.NotNull(bar.Interpretation);
        Assert.NotNull(bar.HedgingEstimate);
        // ZeroDte is null on names without a same-day expiry; don't enforce.
    }

    [Fact]
    public async Task FlashAlphaExposureSummaryBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.ExposureSummaryAsync("SPY", atString);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.ExposureSummaryResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaExposureSummaryBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/summary", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaExposureSummaryBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.GammaFlip, bar.GammaFlip);
        Assert.Equal(raw.Regime, bar.Regime);
        // Nested blocks: compare salient scalars; deep equality of every leaf is
        // intentionally out of scope (drift would surface as a top-level mismatch).
        Assert.Equal(raw.Exposures?.NetGex, bar.Exposures?.NetGex);
        Assert.Equal(raw.Exposures?.NetDex, bar.Exposures?.NetDex);
        Assert.Equal(raw.Exposures?.NetVex, bar.Exposures?.NetVex);
        Assert.Equal(raw.Exposures?.NetChex, bar.Exposures?.NetChex);
        Assert.Equal(raw.Interpretation?.Gamma, bar.Interpretation?.Gamma);
        Assert.Equal(raw.HedgingEstimate?.SpotUp1Pct?.Direction,
                     bar.HedgingEstimate?.SpotUp1Pct?.Direction);
        Assert.Equal(raw.ZeroDte?.Expiration, bar.ZeroDte?.Expiration);
    }

    [Fact]
    public void AddFlashAlphaExposureSummary_ExtensionExists()
    {
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaExposureSummary(t); };
        Assert.NotNull(ext);
    }
}
