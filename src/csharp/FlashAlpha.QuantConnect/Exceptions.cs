using System;

namespace FlashAlpha.QuantConnect;

public class FlashAlphaQuantConnectException : Exception
{
    public string ErrorCode { get; }
    public string DocUrl => $"https://github.com/FlashAlpha-lab/flashalpha-quantconnect/blob/main/docs/troubleshooting.md#{ErrorCode.ToLowerInvariant()}";

    protected FlashAlphaQuantConnectException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}

public sealed class FlashAlphaAuthMissingException : FlashAlphaQuantConnectException
{
    public FlashAlphaAuthMissingException()
        : base("FA-AUTH-001", "FlashAlpha API key not found. Set FLASHALPHA_API_KEY env var, FlashAlphaConfig.ApiKey, or QC parameter 'flashalpha-api-key'.") { }
}

public sealed class FlashAlphaUnauthorizedException : FlashAlphaQuantConnectException
{
    public FlashAlphaUnauthorizedException(string endpoint, string keyTail4)
        : base("FA-AUTH-002", $"FlashAlpha rejected the API key (…{keyTail4}) on endpoint {endpoint}. See troubleshooting docs.") { }
}

public sealed class FlashAlphaRateLimitedException : FlashAlphaQuantConnectException
{
    public FlashAlphaRateLimitedException(string endpoint, Exception inner)
        : base("FA-RATE-001", $"FlashAlpha rate-limited the request to {endpoint} after retries exhausted.", inner) { }
}

public sealed class FlashAlphaNetworkException : FlashAlphaQuantConnectException
{
    public FlashAlphaNetworkException(string endpoint, Exception inner)
        : base("FA-NET-001", $"Network error talking to FlashAlpha at {endpoint}.", inner) { }
}
