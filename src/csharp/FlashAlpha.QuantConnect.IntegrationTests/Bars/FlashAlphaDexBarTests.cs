using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Integration tests for <see cref="FlashAlphaDexBar"/> — Layer 1 + Layer 2 + sugar.
/// </summary>
/// <remarks>
/// Mirrors the GEX bar test file shape: one fetch+parse smoke test, one
/// price-correctness field-by-field compare against the raw SDK response,
/// and one compile-time sugar check.
/// </remarks>
[Trait("Category", "Integration")]
public class FlashAlphaDexBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaDexBar));

    [Fact]
    public void FlashAlphaDexBar_FetchAndParse_PopulatesFields()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/dex", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaDexBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.True(bar.NetDex.HasValue, "NetDex should populate");
        Assert.NotNull(bar.Strikes);
    }

    [Fact]
    public async Task FlashAlphaDexBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.DexAsync("SPY", atString, expiration: null);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.DexResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaDexBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/dex", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaDexBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.NetDex, bar.NetDex);
        Assert.NotNull(bar.Strikes);
        Assert.Equal(raw.Strikes?.Count ?? 0, bar.Strikes!.Count);
    }

    [Fact]
    public void AddFlashAlphaDex_ExtensionExists()
    {
        // Compile-time check: the extension exists and resolves to AddData<FlashAlphaDexBar>.
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaDex(t); };
        Assert.NotNull(ext);
    }
}
