using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaTickersBar"/> — Layer 1 + Layer 2 + sugar.
/// Special-case bar: the upstream endpoint is NOT ticker-scoped, so the bar
/// subscribes under a sentinel symbol and the API returns the global coverage
/// table. The HTTP client passes <c>symbol: null</c> regardless of the LEAN
/// symbol on the subscription.
/// </summary>
[Trait("Category", "Integration")]
public class FlashAlphaTickersBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("_universe", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaTickersBar));

    [Fact]
    public void FlashAlphaTickersBar_FetchAndParse_PopulatesTickers()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("tickers", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaTickersBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.NotNull(bar.Tickers);
        Assert.True(bar.Tickers!.Count > 0, "Tickers list should populate");
        Assert.True(bar.Count.HasValue && bar.Count > 0, "Count should populate");
        // SPY is always in the coverage table.
        var hasSpy = bar.Tickers.Exists(t => t.Symbol == "SPY");
        Assert.True(hasSpy, "SPY should appear in the coverage table");
    }

    [Fact]
    public async Task FlashAlphaTickersBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        // Tickers special-case: no `at` parameter, `symbol: null` for the list form.
        var rawElement = await sdk.TickersAsync(symbol: null);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.TickersResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("_universe", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaTickersBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("tickers", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaTickersBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Count, bar!.Count);
        Assert.Equal(raw.Tickers?.Count ?? 0, bar.Tickers?.Count ?? 0);
        if ((raw.Tickers?.Count ?? 0) > 0)
        {
            // Spot-check the first row.
            Assert.Equal(raw.Tickers![0].Symbol, bar.Tickers![0].Symbol);
            Assert.Equal(raw.Tickers[0].Coverage?.First, bar.Tickers[0].Coverage?.First);
            Assert.Equal(raw.Tickers[0].Coverage?.Last, bar.Tickers[0].Coverage?.Last);
            Assert.Equal(raw.Tickers[0].Coverage?.HealthyDays, bar.Tickers[0].Coverage?.HealthyDays);
        }
    }

    [Fact]
    public void AddFlashAlphaTickers_ExtensionExists()
    {
        System.Action<QCAlgorithm> ext = a => { a.AddFlashAlphaTickers(); };
        Assert.NotNull(ext);
    }
}
