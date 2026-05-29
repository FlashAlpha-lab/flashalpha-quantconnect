using System;
using FlashAlpha.QuantConnect;
using FlashAlpha.QuantConnect.Data;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Orders;

namespace FlashAlpha.QuantConnect.IntegrationTests.EndToEnd;

/// <summary>
/// A minimal, real LEAN algorithm that exercises the FlashAlpha bridge
/// end-to-end: subscribe to GEX, equity, and rebalance based on the GEX
/// regime (long-gamma vs short-gamma).
/// </summary>
/// <remarks>
/// <para><b>Strategy (intentionally simple — this is a fixture, not a
/// production trade):</b></para>
/// <list type="bullet">
///   <item>Long SPY when <see cref="FlashAlphaGexBar.NetGex"/> &gt; 0 (positive-gamma regime).</item>
///   <item>Flat when net GEX flips negative (gamma is short — dealers amplify moves).</item>
/// </list>
///
/// <para>The point of this algorithm is to drive a backtest whose final
/// equity, trade count, and Sharpe can be compared to a committed golden
/// file (<c>golden/end_to_end.json</c>) — surfacing regressions when the
/// SDK or bar layer changes a number.</para>
///
/// <para>This file is committed and compile-checked, but the actual
/// backtest is run by the LEAN engine — not by xUnit's harness. See
/// <see cref="EndToEndBacktestTests"/> for the assertion shape.</para>
/// </remarks>
public class GexRegimeFollowingAlgorithm : QCAlgorithm
{
    private Symbol _equity = null!;
    private Symbol _gex = null!;

    /// <summary>
    /// LEAN entry-point. Wires the date range, cash, and FlashAlpha
    /// subscriptions. The dates are short on purpose — golden capture
    /// runs in a couple of minutes.
    /// </summary>
    public override void Initialize()
    {
        SetStartDate(2024, 6, 3);
        SetEndDate(2024, 6, 14);
        SetCash(100_000);

        _equity = AddEquity("SPY", Resolution.Daily).Symbol;
        _gex = this.AddFlashAlphaGex("SPY").Symbol;
    }

    /// <summary>
    /// LEAN per-bar callback. Rebalances on each fresh GEX print.
    /// </summary>
    public override void OnData(Slice slice)
    {
        if (!slice.ContainsKey(_gex)) return;
        if (slice[_gex] is not FlashAlphaGexBar gex || gex.NetGex is null) return;

        // Long-gamma regime: dealers absorb volatility — comfortable being
        // long. Short-gamma regime: dealers amplify moves — flatten.
        var targetWeight = gex.NetGex > 0 ? 1.0m : 0.0m;
        SetHoldings(_equity, targetWeight);
    }

    /// <summary>Log filled orders — helps diff against the trade-count golden.</summary>
    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        if (orderEvent.Status == OrderStatus.Filled)
        {
            Log($"FILL: {orderEvent.Symbol.Value} {orderEvent.FillQuantity} @ {orderEvent.FillPrice}");
        }
    }
}
