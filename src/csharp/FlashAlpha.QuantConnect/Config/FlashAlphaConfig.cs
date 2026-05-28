using System;

namespace FlashAlpha.QuantConnect;

public static class FlashAlphaConfig
{
    public static string? ApiKey { get; set; }
    public static string BaseUrl { get; set; } = "https://historical.flashalpha.com";
    public static TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public static int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Resolution order: explicit ApiKey property → QC GetParameter("flashalpha-api-key")
    /// → FLASHALPHA_API_KEY env var. Throws FlashAlphaAuthMissingException if all miss.
    /// </summary>
    public static string ResolveApiKey(Func<string, string?>? qcGetParameter = null)
    {
        if (!string.IsNullOrEmpty(ApiKey)) return ApiKey!;

        var fromQc = qcGetParameter?.Invoke("flashalpha-api-key");
        if (!string.IsNullOrEmpty(fromQc)) return fromQc!;

        var fromEnv = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        if (!string.IsNullOrEmpty(fromEnv)) return fromEnv!;

        throw new FlashAlphaAuthMissingException();
    }

    /// <summary>Reset state — for tests only.</summary>
    public static void Reset()
    {
        ApiKey = null;
        BaseUrl = "https://historical.flashalpha.com";
        HttpTimeout = TimeSpan.FromSeconds(30);
        MaxRetries = 3;
    }
}
