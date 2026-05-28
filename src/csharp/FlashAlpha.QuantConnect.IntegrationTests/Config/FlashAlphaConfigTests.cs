using System;
using FlashAlpha.QuantConnect;
using Xunit;

namespace FlashAlpha.QuantConnect.IntegrationTests.Config;

[Trait("Category", "Integration")]
[Collection("FlashAlphaConfig")]
public class FlashAlphaConfigTests : IDisposable
{
    private readonly string? _savedEnv;

    public FlashAlphaConfigTests()
    {
        _savedEnv = Environment.GetEnvironmentVariable("FLASHALPHA_API_KEY");
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", null);
        FlashAlphaConfig.Reset();
    }

    public void Dispose()
    {
        FlashAlphaConfig.Reset();
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", _savedEnv);
    }

    [Fact]
    public void ResolveApiKey_PrefersExplicitOverride()
    {
        FlashAlphaConfig.ApiKey = "explicit-key";
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", "env-key");

        Assert.Equal("explicit-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));
    }

    [Fact]
    public void ResolveApiKey_FallsBackToQCParameter()
    {
        Assert.Equal("qc-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: key =>
            key == "flashalpha-api-key" ? "qc-key" : null));
    }

    [Fact]
    public void ResolveApiKey_FallsBackToEnvVar()
    {
        Environment.SetEnvironmentVariable("FLASHALPHA_API_KEY", "env-key");

        Assert.Equal("env-key", FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));
    }

    [Fact]
    public void ResolveApiKey_ThrowsWhenAllSourcesMiss()
    {
        Assert.Throws<FlashAlphaAuthMissingException>(() =>
            FlashAlphaConfig.ResolveApiKey(qcGetParameter: _ => null));
    }
}

[CollectionDefinition("FlashAlphaConfig", DisableParallelization = true)]
public class FlashAlphaConfigCollection { }
