using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Securities;

namespace FlashAlpha.QuantConnect;

/// <summary>
/// Sugar extensions on <see cref="QCAlgorithm"/> for subscribing to FlashAlpha bars.
/// </summary>
/// <remarks>
/// Each method is a thin one-liner wrapper around <c>AddData&lt;TBar&gt;(ticker, resolution)</c>
/// so users can write <c>algo.AddFlashAlphaGex("SPY")</c> instead of
/// <c>algo.AddData&lt;FlashAlphaGexBar&gt;("SPY", Resolution.Daily)</c>.
/// </remarks>
public static class QCAlgorithmExtensions
{
    /// <summary>
    /// Subscribe to FlashAlpha gamma exposure (GEX) bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the GEX subscription.</returns>
    public static Security AddFlashAlphaGex(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaGexBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha delta exposure (DEX) bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the DEX subscription.</returns>
    public static Security AddFlashAlphaDex(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaDexBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha vanna exposure (VEX) bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the VEX subscription.</returns>
    public static Security AddFlashAlphaVex(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaVexBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha charm exposure (CHEX) bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the CHEX subscription.</returns>
    public static Security AddFlashAlphaChex(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaChexBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha exposure-summary bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the exposure-summary subscription.</returns>
    public static Security AddFlashAlphaExposureSummary(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaExposureSummaryBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha exposure-levels bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the exposure-levels subscription.</returns>
    public static Security AddFlashAlphaExposureLevels(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaExposureLevelsBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha volatility-surface bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the surface subscription.</returns>
    public static Security AddFlashAlphaSurface(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaSurfaceBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha zero-DTE (same-day-expiry) bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the zero-DTE subscription.</returns>
    public static Security AddFlashAlphaZeroDte(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaZeroDteBar>(ticker, resolution);

    /// <summary>
    /// Subscribe to FlashAlpha max-pain bars for the given ticker.
    /// </summary>
    /// <param name="algo">The hosting algorithm.</param>
    /// <param name="ticker">Underlying ticker (e.g. <c>"SPY"</c>).</param>
    /// <param name="resolution">Bar resolution; defaults to <see cref="Resolution.Daily"/>.</param>
    /// <returns>The <see cref="Security"/> registered for the max-pain subscription.</returns>
    public static Security AddFlashAlphaMaxPain(
        this QCAlgorithm algo,
        string ticker,
        Resolution resolution = Resolution.Daily)
        => algo.AddData<FlashAlphaMaxPainBar>(ticker, resolution);
}
