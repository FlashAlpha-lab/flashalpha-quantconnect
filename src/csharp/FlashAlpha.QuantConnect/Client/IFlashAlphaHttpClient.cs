using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlashAlpha.QuantConnect.Client;

/// <summary>
/// Thin façade over the FlashAlpha.Historical SDK. Bar classes call this to
/// fetch the raw JSON response for a (endpoint, ticker, at) triple. Mockable
/// for tests that want to bypass the real API.
/// </summary>
/// <remarks>
/// The 17 supported <c>endpoint</c> slugs match the REST API paths under
/// <c>/v1/</c>: <c>tickers</c>, <c>stockquote</c>, <c>optionquote</c>,
/// <c>surface</c>, <c>exposure/gex</c>, <c>exposure/dex</c>, <c>exposure/vex</c>,
/// <c>exposure/chex</c>, <c>exposure/summary</c>, <c>exposure/levels</c>,
/// <c>exposure/narrative</c>, <c>exposure/zero-dte</c>, <c>maxpain</c>,
/// <c>max-pain</c> (alias for <c>maxpain</c>), <c>stock/summary</c>,
/// <c>volatility</c>, <c>adv_volatility</c>, <c>vrp</c>.
/// </remarks>
public interface IFlashAlphaHttpClient
{
    /// <summary>
    /// Fetch the raw JSON response body for the given endpoint, ticker, and as-of
    /// timestamp. The returned string is the same shape as the live REST API would
    /// have returned at that moment in history — to be parsed by the bridge's
    /// custom-data Bar Reader implementations.
    /// </summary>
    /// <param name="endpoint">Endpoint slug (see remarks for the supported list).</param>
    /// <param name="ticker">Underlying symbol (e.g. "SPY"). Ignored for the <c>tickers</c> endpoint.</param>
    /// <param name="at">As-of timestamp. Wall-clock Eastern Time per the API contract.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw JSON body returned by the FlashAlpha Historical API.</returns>
    Task<string> FetchJsonAsync(string endpoint, string ticker, DateTime at, CancellationToken ct = default);
}
