namespace GoshehYar.Core.Calc;

/// <summary>Three everyday percent calculations. Methods return null when the input makes no sense (division by zero, overflow).</summary>
public static class PercentTools
{
    /// <summary>X% of Y.</summary>
    public static decimal? PercentOf(decimal percent, decimal value) => Safe(() => percent * value / 100m);

    /// <summary>What percent is X of Y.</summary>
    public static decimal? WhatPercent(decimal part, decimal whole) =>
        whole == 0m ? null : Safe(() => part / whole * 100m);

    /// <summary>Y increased by X%.</summary>
    public static decimal? Increase(decimal value, decimal percent) => Safe(() => value + value * percent / 100m);

    /// <summary>Y decreased by X%.</summary>
    public static decimal? Decrease(decimal value, decimal percent) => Safe(() => value - value * percent / 100m);

    private static decimal? Safe(Func<decimal> calc)
    {
        try
        {
            return ExpressionEvaluator.RoundSignificant(calc());
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
