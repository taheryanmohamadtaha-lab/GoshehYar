namespace GoshehYar.Core.Calc;

/// <summary>Bank profit and loan calculations. Every method returns null for invalid or overflowing input.</summary>
public static class FinanceTools
{
    private const double Max = 1e24;

    private static decimal? ToMoney(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > Max) return null;
        return Math.Round((decimal)value, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>Simple interest for a number of days: principal × rate × days / (basis × 100).</summary>
    public static decimal? SimpleInterest(decimal principal, decimal annualPercent, decimal days, int basis = 365)
    {
        if (principal < 0 || annualPercent < 0 || days < 0 || basis <= 0) return null;
        return ToMoney((double)principal * (double)annualPercent * (double)days / (basis * 100.0));
    }

    /// <summary>Final amount with compounding <paramref name="periodsPerYear"/> times a year.</summary>
    public static decimal? CompoundFinal(decimal principal, decimal annualPercent, decimal years, int periodsPerYear)
    {
        if (principal < 0 || annualPercent < 0 || years < 0 || periodsPerYear <= 0) return null;
        var n = (double)periodsPerYear;
        var factor = Math.Pow(1 + (double)annualPercent / 100.0 / n, n * (double)years);
        return ToMoney((double)principal * factor);
    }

    /// <summary>The real yearly rate (percent) when a nominal annual rate is compounded.</summary>
    public static decimal? EffectiveAnnualPercent(decimal annualPercent, int periodsPerYear)
    {
        if (annualPercent < 0 || periodsPerYear <= 0) return null;
        var n = (double)periodsPerYear;
        return ToMoney((Math.Pow(1 + (double)annualPercent / 100.0 / n, n) - 1) * 100.0);
    }

    public static decimal? DailyPercent(decimal annualPercent, int basis = 365) =>
        annualPercent < 0 || basis <= 0 ? null : ToMoney((double)annualPercent / basis);

    public static decimal? MonthlyPercent(decimal annualPercent) =>
        annualPercent < 0 ? null : ToMoney((double)annualPercent / 12.0);

    public static decimal? AnnualFromDailyPercent(decimal dailyPercent, int basis = 365) =>
        dailyPercent < 0 || basis <= 0 ? null : ToMoney((double)dailyPercent * basis);

    public static decimal? AnnualFromMonthlyPercent(decimal monthlyPercent) =>
        monthlyPercent < 0 ? null : ToMoney((double)monthlyPercent * 12.0);

    /// <summary>Savings with a start amount and a deposit at the end of every month; interest compounds monthly.</summary>
    public static decimal? SavingsFinal(decimal initial, decimal monthlyDeposit, decimal annualPercent, int months)
    {
        if (initial < 0 || monthlyDeposit < 0 || annualPercent < 0 || months < 0 || months > 1200) return null;
        var r = (double)annualPercent / 100.0 / 12.0;
        var growth = Math.Pow(1 + r, months);
        var annuity = r == 0 ? months : (growth - 1) / r;
        return ToMoney((double)initial * growth + (double)monthlyDeposit * annuity);
    }

    /// <summary>Fixed monthly installment of a loan (annuity formula).</summary>
    public static decimal? LoanPayment(decimal principal, decimal annualPercent, int months)
    {
        if (principal < 0 || annualPercent < 0 || months <= 0 || months > 1200) return null;
        var r = (double)annualPercent / 100.0 / 12.0;
        if (r == 0) return ToMoney((double)principal / months);
        var growth = Math.Pow(1 + r, months);
        return ToMoney((double)principal * r * growth / (growth - 1));
    }
}
