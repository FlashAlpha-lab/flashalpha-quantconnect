using System;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

[Trait("Category", "Integration")]
public class FlashAlphaGexBarTests
{
    private static readonly DateTime TestDate = new(2024, 6, 14, 15, 30, 0);
    private static readonly Symbol TestSymbol = Symbol.Create("SPY", SecurityType.Base, Market.USA, baseDataType: typeof(FlashAlphaGexBar));

    [Fact]
    public void FlashAlphaGexBar_FetchAndParse_PopulatesFields()
    {
        // Skip the test entirely if no API key — the spec requires real-API integration.
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        FlashAlphaSource.Reset();
        var source = FlashAlphaSource.For("exposure/gex", TestSymbol, TestDate);

        Assert.NotNull(source);
        Assert.StartsWith("flashalpha://", source.Source);

        var bar = FlashAlphaSource.Parse<FlashAlphaGexBar>(source.Source, TestSymbol, TestDate);

        Assert.NotNull(bar);
        Assert.Equal(TestSymbol, bar!.Symbol);
        Assert.Equal(TestDate, bar.Time);
        Assert.True(bar.UnderlyingPrice > 0, "UnderlyingPrice should populate");
        Assert.False(string.IsNullOrEmpty(bar.AsOf), "AsOf should populate");
        Assert.True(bar.NetGex != 0, "NetGex should populate (any nonzero value indicates parse worked)");
        Assert.False(string.IsNullOrEmpty(bar.NetGexLabel), "NetGexLabel should populate");
        Assert.Contains(bar.NetGexLabel, new[] { "positive", "negative", "neutral" });
    }
}
