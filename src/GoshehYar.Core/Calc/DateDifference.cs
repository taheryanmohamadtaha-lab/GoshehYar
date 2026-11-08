namespace GoshehYar.Core.Calc;

public readonly record struct DateDifferenceResult(int Years, int Months, int Days, int TotalDays, bool Reversed);

/// <summary>Difference between two dates (Gregorian arithmetic, time of day ignored). Order does not matter; <c>Reversed</c> tells the dates were swapped.</summary>
public static class DateDifference
{
    public static DateDifferenceResult Compute(DateTime a, DateTime b)
    {
        var reversed = a.Date > b.Date;
        var from = reversed ? b.Date : a.Date;
        var to = reversed ? a.Date : b.Date;

        var totalMonths = (to.Year - from.Year) * 12 + (to.Month - from.Month);
        while (totalMonths > 0 && from.AddMonths(totalMonths) > to) totalMonths--;
        var days = (to - from.AddMonths(totalMonths)).Days;

        return new DateDifferenceResult(totalMonths / 12, totalMonths % 12, days, (to - from).Days, reversed);
    }
}
