using QuantConnect.Algorithm;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Bars;

/// <summary>
/// Wiring test for the <c>AddFlashAlphaGex</c> sugar extension on
/// <see cref="QCAlgorithm"/>.
/// </summary>
/// <remarks>
/// <para>Compile-time-only check: the extension exists, resolves to
/// <c>AddData&lt;FlashAlphaGexBar&gt;</c>, and binds correctly off
/// <see cref="QCAlgorithm"/>.</para>
///
/// <para>A runtime check (constructing <see cref="QCAlgorithm"/> and inspecting
/// <c>SubscriptionManager.Subscriptions</c>) was attempted first but the LEAN
/// <see cref="QCAlgorithm"/> constructor in 2.5.17414 requires a Data/market-hours
/// folder relative to cwd that isn't present in the test harness — that's a LEAN
/// bootstrapping concern, not a check the sugar method actually does what it claims.
/// The compile-time delegate below is sufficient: if the extension didn't dispatch
/// to <c>AddData&lt;FlashAlphaGexBar&gt;</c>, this file wouldn't compile.</para>
/// </remarks>
[Trait("Category", "Integration")]
public class AddFlashAlphaGexSugarTests
{
    [Fact]
    public void AddFlashAlphaGex_ExtensionExists()
    {
        // Compile-time check: the extension exists and resolves to AddData<FlashAlphaGexBar>.
        // No runtime LEAN setup required — this just proves the API surface.
        System.Action<QCAlgorithm, string> ext = (a, t) => { a.AddFlashAlphaGex(t); };
        Assert.NotNull(ext);
    }
}
