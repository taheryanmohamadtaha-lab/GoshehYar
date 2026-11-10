using System.Globalization;

namespace GoshehYar.Core.Calc;

/// <summary>Jalali (Shamsi) ↔ Gregorian conversion. Never throws; invalid dates give null.</summary>
public static class DateConverter
{
    private static readonly PersianCalendar Persian = new();

    /// <summary>Parses a whole number typed with Persian, Arabic or English digits.</summary>
    public static bool TryParseInt(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var plain = ExpressionEvaluator.Normalize(text.Trim());
        return int.TryParse(plain, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public static bool IsValidJalali(int year, int month, int day)
    {
        if (year < 1 || year > 9377 || month < 1 || month > 12 || day < 1) return false;
        try
        {
            return day <= Persian.GetDaysInMonth(year, month);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static DateTime? JalaliToGregorian(int year, int month, int day)
    {
        if (!IsValidJalali(year, month, day)) return null;
        try
        {
            return Persian.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static (int Year, int Month, int Day)? GregorianToJalali(int year, int month, int day)
    {
        if (year < 623 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return null;
        try
        {
            var d = new DateTime(year, month, day);
            return (Persian.GetYear(d), Persian.GetMonth(d), Persian.GetDayOfMonth(d));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>First and last Gregorian day of a Jalali year (1 Farvardin to the last day of Esfand).</summary>
    public static (DateTime Start, DateTime End)? JalaliYearToGregorian(int year)
    {
        var start = JalaliToGregorian(year, 1, 1);
        if (start is null) return null;
        var lastDay = Persian.GetDaysInMonth(year, 12);
        var end = JalaliToGregorian(year, 12, lastDay);
        return end is null ? null : (start.Value, end.Value);
    }

    /// <summary>The Jalali dates of the first and last day of a Gregorian year.</summary>
    public static ((int Year, int Month, int Day) Start, (int Year, int Month, int Day) End)? GregorianYearToJalali(int year)
    {
        var a = GregorianToJalali(year, 1, 1);
        var b = GregorianToJalali(year, 12, 31);
        return a is null || b is null ? null : (a.Value, b.Value);
    }

    public static bool IsJalaliLeap(int year)
    {
        try
        {
            return Persian.IsLeapYear(year);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
