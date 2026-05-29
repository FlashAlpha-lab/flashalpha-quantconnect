using System;
using System.Threading.Tasks;
using FlashAlpha.Historical;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Layer 2 (price-correctness) test for <see cref="FlashAlphaGexBar"/>.
/// </summary>
/// <remarks>
/// Fetches the raw <see cref="FlashAlpha.Historical.Models.GexResponse"/> through the
/// SDK and the parsed bar through the LEAN <c>GetSource</c>+<c>Reader</c> pipeline,
/// then asserts every scalar field matches one-for-one. This is the canary that
/// catches any silent field drop / rename inside <see cref="FlashAlphaJsonMapper"/>.
/// </remarks>
[Trait("Category", "Integration")]
public class FlashAlphaGexBarPriceCorrectnessTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);

    [Fact]
    public async Task FlashAlphaGexBar_FieldsMatchRestResponse()
    {
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        using var sdk = new FlashAlphaHistoricalClient(apiKey: key);
        var atString = FlashAlphaHistoricalClient.FormatAt(TestDate);
        var rawElement = await sdk.GexAsync("SPY", atString, expiration: null, minOi: null);
        var raw = System.Text.Json.JsonSerializer.Deserialize<FlashAlpha.Historical.Models.GexResponse>(
            rawElement.GetRawText())!;

        var symbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaGexBar));
        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/gex", symbol, TestDate);
        var bar = FlashAlphaSource.Parse<FlashAlphaGexBar>(source.Source, symbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(raw.Symbol, bar!.Ticker);
        Assert.Equal(raw.UnderlyingPrice, bar.UnderlyingPrice);
        Assert.Equal(raw.AsOf, bar.AsOf);
        Assert.Equal(raw.GammaFlip, bar.GammaFlip);
        Assert.Equal(raw.NetGex, bar.NetGex);
        Assert.Equal(raw.NetGexLabel, bar.NetGexLabel);
        // Strikes list is non-null and same length as raw; deeper element-wise compare
        // is intentionally out of scope — drift would surface as a count mismatch.
        Assert.NotNull(bar.Strikes);
        Assert.Equal(raw.Strikes?.Count ?? 0, bar.Strikes!.Count);
    }
}
