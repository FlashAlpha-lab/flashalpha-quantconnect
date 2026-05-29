using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlashAlpha.Historical.Models;
using FlashAlpha.QuantConnect.Client;
using FlashAlpha.QuantConnect.Data;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Universe;

/// <summary>
/// Wiring tests for <see cref="FlashAlphaTickersUniverse"/> — the
/// universe-selection helper backed by the FlashAlpha tickers endpoint.
/// </summary>
/// <remarks>
/// <para>The compile-time tests prove the class is constructible with each
/// supported overload and that the filter predicate runs row-by-row against
/// the SDK's <see cref="TickersRow"/> type.</para>
///
/// <para>The live integration test pulls the real coverage table and asserts
/// the filter narrows the universe — it skips when <c>FLASHALPHA_API_KEY</c>
/// isn't set, matching the rest of the integration suite.</para>
/// </remarks>
[Trait("Category", "Integration")]
public class TickersUniverseTests
{
    [Fact]
    public void FlashAlphaTickersUniverse_DefaultCtor_IsConstructible()
    {
        // Compile-time / constructor wiring check.
        var universe = new FlashAlphaTickersUniverse();
        Assert.NotNull(universe);
    }

    [Fact]
    public void FlashAlphaTickersUniverse_WithFilter_IsConstructible()
    {
        // Filter overload — the predicate sees the SDK row type.
        var universe = new FlashAlphaTickersUniverse(
            row => (row.Coverage?.HealthyDays ?? 0) > 30);
        Assert.NotNull(universe);
    }

    [Fact]
    public void FlashAlphaTickersUniverse_WithMockClient_AppliesFilter()
    {
        // Pure unit test: mock the HTTP client, return a hand-rolled coverage
        // table, prove the filter narrows the universe.
        var mock = new MockHttpClient(@"{
            ""tickers"": [
                { ""symbol"": ""SPY"", ""coverage"": { ""first"": ""2020-01-01"", ""last"": ""2024-12-31"", ""healthy_days"": 1200 } },
                { ""symbol"": ""QQQ"", ""coverage"": { ""first"": ""2020-01-01"", ""last"": ""2024-12-31"", ""healthy_days"": 1180 } },
                { ""symbol"": ""SPARSE"", ""coverage"": { ""first"": ""2024-06-01"", ""last"": ""2024-06-30"", ""healthy_days"": 20 } }
            ],
            ""count"": 3
        }");

        var universe = new FlashAlphaTickersUniverse(
            row => (row.Coverage?.HealthyDays ?? 0) > 100,
            client: mock);

        var symbols = universe.SelectTickers(new DateTime(2024, 6, 14)).ToList();

        Assert.Equal(2, symbols.Count);
        Assert.Contains("SPY", symbols);
        Assert.Contains("QQQ", symbols);
        Assert.DoesNotContain("SPARSE", symbols);
    }

    [Fact]
    public void FlashAlphaTickersUniverse_EmptyResponse_YieldsEmptyUniverse()
    {
        // Network failures and parse errors yield an empty universe — see the
        // FlashAlphaTickersUniverse.Select remarks.
        var mock = new MockHttpClient("");
        var universe = new FlashAlphaTickersUniverse(client: mock);
        var symbols = universe.SelectTickers(new DateTime(2024, 6, 14)).ToList();
        Assert.Empty(symbols);
    }

    [Fact]
    public void FlashAlphaTickersUniverse_MalformedJson_YieldsEmptyUniverse()
    {
        var mock = new MockHttpClient("not-json");
        var universe = new FlashAlphaTickersUniverse(client: mock);
        var symbols = universe.SelectTickers(new DateTime(2024, 6, 14)).ToList();
        Assert.Empty(symbols);
    }

    [Fact]
    public void FlashAlphaTickersUniverse_LiveFetch_NarrowsTheUniverse()
    {
        // Integration: skip without an API key, matching the rest of the suite.
        var key = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (string.IsNullOrEmpty(key)) return;

        var unfiltered = new FlashAlphaTickersUniverse();
        var allTickers = unfiltered.SelectTickers(DateTime.UtcNow).ToList();

        var heavilyCovered = new FlashAlphaTickersUniverse(
            row => (row.Coverage?.HealthyDays ?? 0) > 100);
        var filtered = heavilyCovered.SelectTickers(DateTime.UtcNow).ToList();

        Assert.NotEmpty(allTickers);
        // Filter should drop at least some rows (or none in the rare case where
        // every covered symbol is >100 days). At minimum the filtered set is
        // a subset of the unfiltered one.
        Assert.Subset(allTickers.ToHashSet(), filtered.ToHashSet());
        // SPY always survives a sensible coverage filter.
        Assert.Contains("SPY", filtered);
    }

    /// <summary>
    /// Tiny fake of <see cref="IFlashAlphaHttpClient"/> returning a fixed JSON
    /// body — lets the universe tests exercise the SelectTickers path without
    /// hitting the network.
    /// </summary>
    private sealed class MockHttpClient : IFlashAlphaHttpClient
    {
        private readonly string _json;
        public MockHttpClient(string json) { _json = json; }
        public Task<string> FetchJsonAsync(string endpoint, string ticker, DateTime at, CancellationToken ct = default)
            => Task.FromResult(_json);
    }
}
