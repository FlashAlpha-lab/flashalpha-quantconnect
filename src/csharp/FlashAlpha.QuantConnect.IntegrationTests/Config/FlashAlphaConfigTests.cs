using System;
using FlashAlpha.QuantConnect;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Config;

[Trait("Category", "Integration")]
public class FlashAlphaConfigTests
{
    [Fact]
    public void ResolveApiKey_PrefersExplicitOverride()
    {
        FlashAlphaConfig.Reset();
        FlashAlphaConfig.ApiKey = "explicit-key";
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", "env-key");

        Assert.Equal("explicit-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));

        FlashAlphaConfig.Reset();
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", null);
    }

    [Fact]
    public void ResolveApiKey_FallsBackToQCParameter()
    {
        FlashAlphaConfig.Reset();
        Assert.Equal("qc-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: key =>
            key == "flashalpha-api-key" ? "qc-key" : null));
    }

    [Fact]
    public void ResolveApiKey_FallsBackToEnvVar()
    {
        FlashAlphaConfig.Reset();
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", "env-key");

        Assert.Equal("env-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));

        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", null);
    }

    [Fact]
    public void ResolveApiKey_ThrowsWhenAllSourcesMiss()
    {
        FlashAlphaConfig.Reset();
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", null);

        Assert.Throws<FlashAlphaAuthMissingException>(() =>
            FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));
    }
}
