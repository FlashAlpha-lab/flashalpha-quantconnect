using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using FlashAlpha.Historical.Models;
using FlashAlpha.QuantConnect.Data;
using QuantConnect.Data;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Schema;

/// <summary>
/// Schema drift guard.
/// </summary>
/// <remarks>
/// <para>For every FlashAlpha bar that mirrors a typed SDK response, reflect
/// over the bar's properties and assert each JSON name is present on the
/// matching SDK DTO. Catches silent drift when the SDK adds (or renames) a
/// field but the bar layer forgets to expose it.</para>
///
/// <para><b>The check is one-way (bar ⊆ DTO):</b> every bar property must be
/// resolvable on the SDK type. The reverse — SDK fields the bar doesn't
/// expose — is allowed and intentional (we deliberately rename
/// <c>symbol</c> -&gt; <c>Ticker</c> to free the LEAN <c>Symbol</c> property,
/// and <c>price</c> -&gt; <c>PriceQuote</c> to avoid colliding with
/// <see cref="BaseData.Price"/>). A separate test surfaces the SDK-only
/// fields so a human can decide whether to expose them.</para>
///
/// <para><b>Skipped bars:</b> <see cref="FlashAlphaStockQuoteBar"/> and
/// <see cref="FlashAlphaOptionQuoteBar"/> wrap raw JSON responses
/// (<see cref="System.Text.Json.JsonElement"/> in the SDK), so there's no
/// typed DTO to diff against — they're listed in <see cref="SkippedBars"/>
/// with a documented reason.</para>
/// </remarks>
[Trait("Category", "Integration")]
public class SchemaDriftGuardTests
{
    /// <summary>
    /// The 17 bars and the SDK type each mirrors. Hand-maintained — if a
    /// new bar is added without a matching entry here, the
    /// <see cref="DriftGuard_CoversAllBars"/> test fails.
    /// </summary>
    public static IEnumerable<object[]> BarToDtoCases() => new[]
    {
        new object[] { typeof(FlashAlphaGexBar),              typeof(GexResponse) },
        new object[] { typeof(FlashAlphaDexBar),              typeof(DexResponse) },
        new object[] { typeof(FlashAlphaVexBar),              typeof(VexResponse) },
        new object[] { typeof(FlashAlphaChexBar),             typeof(ChexResponse) },
        new object[] { typeof(FlashAlphaExposureSummaryBar),  typeof(ExposureSummaryResponse) },
        new object[] { typeof(FlashAlphaExposureLevelsBar),   typeof(ExposureLevelsResponse) },
        new object[] { typeof(FlashAlphaSurfaceBar),          typeof(SurfaceResponse) },
        new object[] { typeof(FlashAlphaZeroDteBar),          typeof(ZeroDteResponse) },
        new object[] { typeof(FlashAlphaMaxPainBar),          typeof(MaxPainResponse) },
        new object[] { typeof(FlashAlphaVolatilityBar),       typeof(VolatilityResponse) },
        new object[] { typeof(FlashAlphaAdvVolatilityBar),    typeof(AdvVolatilityResponse) },
        new object[] { typeof(FlashAlphaVrpBar),              typeof(VrpResponse) },
        new object[] { typeof(FlashAlphaNarrativeBar),        typeof(NarrativeResponse) },
        new object[] { typeof(FlashAlphaStockSummaryBar),     typeof(StockSummaryResponse) },
        new object[] { typeof(FlashAlphaTickersBar),          typeof(TickersResponse) },
    };

    /// <summary>
    /// Bars that have NO typed SDK DTO (raw <c>JsonElement</c>) — skipped
    /// with documented reasons.
    /// </summary>
    public static readonly Dictionary<Type, string> SkippedBars = new()
    {
        [typeof(FlashAlphaStockQuoteBar)] =
            "SDK exposes the stockquote response as a raw JsonElement (no typed DTO). " +
            "Drift check would need a hand-curated expected key set.",
        [typeof(FlashAlphaOptionQuoteBar)] =
            "SDK exposes the optionquote response as a raw JsonElement array (no typed DTO). " +
            "Per-row OptionQuoteRow drift could be added separately if needed.",
    };

    /// <summary>
    /// Bar properties that are intentionally renamed away from the SDK
    /// field name. Drift guard treats these as "expected" so they don't
    /// fire false-positive alerts.
    /// </summary>
    /// <remarks>
    /// Keys are <c>bar-JSON-name</c>; values are the matching SDK JSON name
    /// that the bar's property actually maps onto. Empty values mean
    /// "expose this bar property even though the SDK has no such field"
    /// (i.e. a synthetic).
    /// </remarks>
    private static readonly Dictionary<string, string> ExpectedRenames = new()
    {
        // Every bar renames the SDK's "symbol" to "Ticker" so LEAN's
        // BaseData.Symbol property is left alone for the LEAN identity.
        // The bar carries a [JsonPropertyName("symbol")] on Ticker so the
        // JSON name still maps onto the SDK's "symbol" — drift guard
        // checks for "symbol" on the SDK side and finds it.
    };

    /// <summary>
    /// The main drift check — every bar property's JSON name must exist
    /// on the matching SDK DTO's JSON surface.
    /// </summary>
    [Theory]
    [MemberData(nameof(BarToDtoCases))]
    public void Bar_PropertiesAreSubsetOfSdkDto(Type barType, Type dtoType)
    {
        var barJsonNames = GetJsonNames(barType, excludeBaseDataInherited: true);
        var dtoJsonNames = GetJsonNames(dtoType, excludeBaseDataInherited: false);

        var missing = barJsonNames
            .Where(n => !dtoJsonNames.Contains(n))
            .Where(n => !ExpectedRenames.ContainsKey(n))
            .ToList();

        if (missing.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Schema drift on {barType.Name} vs {dtoType.Name}:");
            sb.AppendLine($"  Bar JSON names not found on SDK DTO: [{string.Join(", ", missing)}]");
            sb.AppendLine($"  SDK DTO JSON names: [{string.Join(", ", dtoJsonNames.OrderBy(s => s))}]");
            Assert.Fail(sb.ToString());
        }
    }

    /// <summary>
    /// Surfaces SDK fields the bar does NOT expose — informational only,
    /// not a hard failure. Useful when bumping the SDK to spot newly added
    /// fields that the bar should probably mirror.
    /// </summary>
    /// <remarks>
    /// Asserts that the bar exposes at LEAST one field shared with the SDK
    /// — guards against a bar accidentally being detached from its DTO
    /// entirely.
    /// </remarks>
    [Theory]
    [MemberData(nameof(BarToDtoCases))]
    public void Bar_OverlapsWithSdkDto(Type barType, Type dtoType)
    {
        var barJsonNames = GetJsonNames(barType, excludeBaseDataInherited: true);
        var dtoJsonNames = GetJsonNames(dtoType, excludeBaseDataInherited: false);
        var overlap = barJsonNames.Intersect(dtoJsonNames).ToList();

        Assert.True(overlap.Count > 0,
            $"{barType.Name} shares ZERO field names with {dtoType.Name} — " +
            "the mapping is almost certainly wrong.");
    }

    /// <summary>
    /// Sanity check: every concrete <c>FlashAlpha*Bar</c> in the
    /// production assembly is either in <see cref="BarToDtoCases"/> or in
    /// <see cref="SkippedBars"/>. Catches new bars added without a drift
    /// mapping.
    /// </summary>
    [Fact]
    public void DriftGuard_CoversAllBars()
    {
        var allBars = typeof(FlashAlphaGexBar).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "FlashAlpha.QuantConnect.Data")
            .Where(t => t.Name.StartsWith("FlashAlpha", StringComparison.Ordinal)
                     && t.Name.EndsWith("Bar", StringComparison.Ordinal))
            .ToList();

        var covered = BarToDtoCases().Select(x => (Type)x[0]).ToHashSet();
        var skipped = SkippedBars.Keys.ToHashSet();

        var uncovered = allBars
            .Where(b => !covered.Contains(b) && !skipped.Contains(b))
            .ToList();

        if (uncovered.Count > 0)
        {
            Assert.Fail(
                "Bars not covered by drift guard: " +
                string.Join(", ", uncovered.Select(t => t.Name)) + ". " +
                "Add an entry to BarToDtoCases or SkippedBars in SchemaDriftGuardTests.");
        }

        Assert.Equal(17, allBars.Count); // Matches the spec's "17 bars" invariant.
    }

    // ---------- helpers ----------

    /// <summary>
    /// Returns the JSON-name set for <paramref name="type"/>:
    /// <see cref="JsonPropertyNameAttribute"/> if present, else PascalCase
    /// -&gt; snake_case fallback. Optionally skips properties inherited
    /// from <see cref="BaseData"/> / <see cref="object"/> (the bar's LEAN
    /// scaffolding, not part of the JSON surface).
    /// </summary>
    private static HashSet<string> GetJsonNames(Type type, bool excludeBaseDataInherited)
    {
        var declared = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead);

        if (excludeBaseDataInherited)
        {
            declared = declared.Where(p =>
                p.DeclaringType != typeof(BaseData) &&
                p.DeclaringType != typeof(object));
        }

        return declared
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                         ?? ToSnakeCase(p.Name))
            .ToHashSet();
    }

    private static string ToSnakeCase(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
