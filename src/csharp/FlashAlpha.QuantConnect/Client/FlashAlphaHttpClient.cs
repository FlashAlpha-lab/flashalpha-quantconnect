using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlashAlpha.Historical;

namespace FlashAlpha.QuantConnect.Client;

/// <summary>
/// Default <see cref="IFlashAlphaHttpClient"/> backed by the
/// <see cref="FlashAlphaHistoricalClient"/> SDK (which sets the
/// <c>X-Api-Key</c> header internally — see ADR
/// <c>2026-05-28-auth-mechanism-decision.md</c>).
/// </summary>
/// <remarks>
/// Switches on the endpoint slug, calls the matching SDK method, and returns
/// the raw JSON body via <see cref="JsonElement.GetRawText"/>. SDK exceptions
/// are translated to bridge exceptions so the rest of the bridge surface area
/// only sees the typed errors documented in <c>docs/troubleshooting.md</c>.
/// </remarks>
public sealed class FlashAlphaHttpClient : IFlashAlphaHttpClient, IDisposable
{
    private readonly FlashAlphaHistoricalClient _sdk;
    private readonly bool _ownsSdk;
    private readonly string _keyTail4;
    private bool _disposed;

    /// <summary>
    /// Construct using the API key resolved from
    /// <see cref="FlashAlphaConfig.ResolveApiKey(Func{string, string?}?)"/> —
    /// throws <see cref="FlashAlphaAuthMissingException"/> if no key is
    /// configured.
    /// </summary>
    public FlashAlphaHttpClient()
    {
        var key = FlashAlphaConfig.ResolveApiKey();
        _sdk = new FlashAlphaHistoricalClient(
            apiKey: key,
            baseUrl: FlashAlphaConfig.BaseUrl,
            timeout: (int)FlashAlphaConfig.HttpTimeout.TotalSeconds);
        _ownsSdk = true;
        _keyTail4 = TailOf(key);
    }

    /// <summary>
    /// Construct with a pre-built SDK client — used by tests and by callers
    /// that want to share a <see cref="HttpClient"/> across the bridge.
    /// </summary>
    /// <param name="sdk">SDK client to wrap. Not disposed by this wrapper.</param>
    public FlashAlphaHttpClient(FlashAlphaHistoricalClient sdk)
    {
        _sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
        _ownsSdk = false;
        _keyTail4 = "????";
    }

    private static string TailOf(string key)
        => key.Length >= 4 ? key.Substring(key.Length - 4) : "????";

    /// <inheritdoc />
    public async Task<string> FetchJsonAsync(
        string endpoint,
        string ticker,
        DateTime at,
        CancellationToken ct = default)
    {
        if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));
        if (ticker is null && !string.Equals(endpoint, "tickers", StringComparison.Ordinal))
            throw new ArgumentNullException(nameof(ticker));

        // The SDK has DateTime overloads on some endpoints but not all
        // (and not consistently across patch versions of 0.4.x). For
        // forward-compatibility, format the timestamp once via the SDK's
        // own FormatAt helper and pass strings everywhere.
        var atString = FlashAlphaHistoricalClient.FormatAt(at);

        try
        {
            JsonElement element = endpoint switch
            {
                // Coverage
                "tickers"             => await _sdk.TickersAsync(symbol: null, ct).ConfigureAwait(false),

                // Market data — hyphenated forms (stock/quote, option/quote) are
                // the plan-canonical bridge slugs; the SDK's underlying REST
                // path uses unhyphenated stockquote / optionquote.
                "stock/quote"         => await _sdk.StockQuoteAsync(ticker!, atString, ct).ConfigureAwait(false),
                "stockquote"          => await _sdk.StockQuoteAsync(ticker!, atString, ct).ConfigureAwait(false),
                "option/quote"        => await _sdk.OptionQuoteAsync(ticker!, atString, expiry: null, strike: null, type: null, ct).ConfigureAwait(false),
                "optionquote"         => await _sdk.OptionQuoteAsync(ticker!, atString, expiry: null, strike: null, type: null, ct).ConfigureAwait(false),
                "surface"             => await _sdk.SurfaceAsync(ticker!, atString, ct).ConfigureAwait(false),

                // Exposure
                "exposure/gex"        => await _sdk.GexAsync(ticker!, atString, expiration: null, minOi: null, ct).ConfigureAwait(false),
                "exposure/dex"        => await _sdk.DexAsync(ticker!, atString, expiration: null, ct).ConfigureAwait(false),
                "exposure/vex"        => await _sdk.VexAsync(ticker!, atString, expiration: null, ct).ConfigureAwait(false),
                "exposure/chex"       => await _sdk.ChexAsync(ticker!, atString, expiration: null, ct).ConfigureAwait(false),
                "exposure/summary"    => await _sdk.ExposureSummaryAsync(ticker!, atString, ct).ConfigureAwait(false),
                "exposure/levels"     => await _sdk.ExposureLevelsAsync(ticker!, atString, ct).ConfigureAwait(false),

                // Narrative — plan-canonical "narrative" + SDK REST-path "exposure/narrative"
                "narrative"           => await _sdk.NarrativeAsync(ticker!, atString, ct).ConfigureAwait(false),
                "exposure/narrative"  => await _sdk.NarrativeAsync(ticker!, atString, ct).ConfigureAwait(false),

                "exposure/zero-dte"   => await _sdk.ZeroDteAsync(ticker!, atString, strikeRange: null, ct).ConfigureAwait(false),

                // Max pain — plan-canonical "max-pain" + SDK REST-path "maxpain"
                "max-pain"            => await _sdk.MaxPainAsync(ticker!, atString, expiration: null, ct).ConfigureAwait(false),
                "maxpain"             => await _sdk.MaxPainAsync(ticker!, atString, expiration: null, ct).ConfigureAwait(false),

                // Composite
                "stock/summary"       => await _sdk.StockSummaryAsync(ticker!, atString, ct).ConfigureAwait(false),

                // Volatility — plan-canonical "adv-volatility" + SDK REST-path "adv_volatility"
                "volatility"          => await _sdk.VolatilityAsync(ticker!, atString, ct).ConfigureAwait(false),
                "adv-volatility"      => await _sdk.AdvVolatilityAsync(ticker!, atString, ct).ConfigureAwait(false),
                "adv_volatility"      => await _sdk.AdvVolatilityAsync(ticker!, atString, ct).ConfigureAwait(false),

                // VRP
                "vrp"                 => await _sdk.VrpAsync(ticker!, atString, ct).ConfigureAwait(false),

                _ => throw new ArgumentException(
                    $"Unknown FlashAlpha endpoint slug: '{endpoint}'.", nameof(endpoint)),
            };

            return element.GetRawText();
        }
        catch (AuthenticationException)
        {
            throw new FlashAlphaUnauthorizedException(endpoint, _keyTail4);
        }
        catch (RateLimitException ex)
        {
            throw new FlashAlphaRateLimitedException(endpoint, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FlashAlphaNetworkException(endpoint, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient surfaces request timeouts as TaskCanceledException
            // without the caller's token being cancelled.
            throw new FlashAlphaNetworkException(endpoint, ex);
        }
    }

    /// <summary>
    /// Disposes the underlying SDK client if (and only if) this instance
    /// constructed it (i.e. not the injected-SDK constructor overload).
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        if (_ownsSdk) _sdk.Dispose();
        _disposed = true;
    }
}
