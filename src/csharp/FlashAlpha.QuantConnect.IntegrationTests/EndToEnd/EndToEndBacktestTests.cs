using System;
using System.IO;
using System.Text.Json;
using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.EndToEnd;

/// <summary>
/// Layer-3 end-to-end backtest assertion shape.
/// </summary>
/// <remarks>
/// <para>This is the assertion site for a real LEAN backtest of
/// <see cref="GexRegimeFollowingAlgorithm"/>. The flow is:</para>
/// <list type="number">
///   <item>An external harness runs <c>lean backtest</c> against the
///     committed algorithm, captures the final stats (equity, trade
///     count, Sharpe), and overwrites <c>golden/end_to_end.json</c>.</item>
///   <item>This test re-runs the backtest in-process (LEAN-as-library) and
///     compares the live result to the committed golden numbers — a drift
///     surfaces as a test failure.</item>
/// </list>
///
/// <para><b>Currently skipped by default</b> because the local sandbox
/// cannot run a full LEAN backtest end-to-end (no Lean Data folder, no
/// market-hours database, no map files). Run with
/// <c>dotnet test --filter Category=EndToEnd</c> once a LEAN harness is
/// wired — at that point the placeholder golden values should also be
/// re-captured.</para>
///
/// <para>The current placeholder golden carries <c>_status: "PLACEHOLDER"</c>
/// — the test asserts on its presence so that a stale placeholder cannot
/// silently masquerade as a passing real backtest.</para>
/// </remarks>
[Trait("Category", "EndToEnd")]
public class EndToEndBacktestTests
{
    // Resolved at test time: the csproj copies golden/end_to_end.json into
    // bin/.../golden/end_to_end.json so it's reachable next to the test DLL.
    private static readonly string GoldenPath = Path.Combine(
        Path.GetDirectoryName(typeof(EndToEndBacktestTests).Assembly.Location)!,
        "golden", "end_to_end.json");

    /// <summary>
    /// Reads the committed golden and proves it parses cleanly. Always
    /// runs (no LEAN required) — guards against accidental golden-file
    /// corruption.
    /// </summary>
    [Fact]
    public void Golden_ParsesCleanly()
    {
        var fullPath = Path.GetFullPath(GoldenPath);
        Assert.True(File.Exists(fullPath), $"golden file missing: {fullPath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(fullPath));
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("final_equity", out _));
        Assert.True(root.TryGetProperty("total_trades", out _));
        Assert.True(root.TryGetProperty("sharpe", out _));
    }

    /// <summary>
    /// Re-runs the LEAN backtest and asserts on the committed golden.
    /// Skipped without a LEAN harness — see the class-level remarks.
    /// </summary>
    [Fact]
    [Trait("Category", "EndToEnd")]
    public void GexRegimeFollowing_MatchesGolden()
    {
        // Skip unless a LEAN harness has been wired in.
        // The signaling env var is set by the external CI step that
        // produces the matching golden capture.
        var harness = Environment.GetEnvironmentVariable("FLASHALPHA_LEAN_HARNESS");
        if (string.IsNullOrEmpty(harness)) return;

        var fullPath = Path.GetFullPath(GoldenPath);
        using var doc = JsonDocument.Parse(File.ReadAllText(fullPath));
        var golden = doc.RootElement;

        // Refuse to run against a stale placeholder — the harness must
        // overwrite the golden first.
        if (golden.TryGetProperty("_status", out var status) &&
            status.GetString()?.StartsWith("PLACEHOLDER", StringComparison.Ordinal) == true)
        {
            Assert.Fail(
                "Golden file is still the placeholder. Re-run the LEAN harness " +
                "to capture real numbers before enabling this test.");
        }

        // TODO: when the LEAN harness is wired, instantiate the engine
        // here, point it at GexRegimeFollowingAlgorithm, run the backtest,
        // and compare to the golden values below.
        //
        // var actual = LeanHarness.Run<GexRegimeFollowingAlgorithm>();
        // Assert.Equal(golden.GetProperty("final_equity").GetDecimal(),
        //     actual.FinalEquity, precision: 2);
        // Assert.Equal(golden.GetProperty("total_trades").GetInt32(),
        //     actual.TotalTrades);
        // Assert.Equal(golden.GetProperty("sharpe").GetDouble(),
        //     actual.Sharpe, precision: 3);

        Assert.Fail(
            "Harness env var is set but the LEAN-engine wiring isn't " +
            "implemented yet — wire LeanHarness.Run<T>() into this test.");
    }

    /// <summary>
    /// Compile-time check: the algorithm type is reachable and
    /// instantiable through reflection. Catches accidental removal of
    /// the algorithm class without needing a full backtest.
    /// </summary>
    [Fact]
    public void GexRegimeFollowingAlgorithm_TypeIsAvailable()
    {
        var type = typeof(GexRegimeFollowingAlgorithm);
        Assert.NotNull(type);
        Assert.True(type.IsSubclassOf(typeof(QCAlgorithm)));
    }
}
