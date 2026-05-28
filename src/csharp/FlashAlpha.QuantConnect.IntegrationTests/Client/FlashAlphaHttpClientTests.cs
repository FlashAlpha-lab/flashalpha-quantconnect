using System;
using System.Threading.Tasks;
using FlashAlpha.QuantConnect.Client;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Client;

[Trait("Category", "Integration")]
public class FlashAlphaHttpClientTests
{
    private static bool HasKey =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY"));

    [Fact]
    public async Task FetchGexJson_ReturnsNonEmptyJsonForKnownDate()
    {
        // Real API call — requires FLASHALPHA_API_KEY in environment.
        // We skip rather than fail when the key is absent so CI without secrets stays green.
        if (!HasKey)
        {
            // Equivalent of xUnit's SkippableFact pattern without the extra package dep:
            // fail loudly only if the key is present but the call breaks; otherwise return.
            return;
        }

        using var client = new FlashAlphaHttpClient();

        // 2024-08-05 is the canonical golden date used by the SDK's own integration tests.
        var json = await client.FetchJsonAsync(
            endpoint: "exposure/gex",
            ticker: "SPY",
            at: new DateTime(2024, 8, 5, 15, 30, 0));

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("\"net_gex\"", json);
        Assert.Contains("\"symbol\"", json);
    }
}
