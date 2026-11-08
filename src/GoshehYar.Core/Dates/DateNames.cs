using System.Globalization;
using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Dates;

/// <summary>Shared date helpers for the clock and the calendar: Jalali (Persian) and Gregorian names and conversion.</summary>
public static class DateNames
{
    private static readonly PersianCalendar Persian = new();

    public static readonly string[] JalaliMonthsFa =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };

    public static readonly string[] GregorianMonthsFa =
    {
        "ژانویه", "فوریه", "مارس", "آوریل", "مه", "ژوئن",
        "ژوئیه", "اوت", "سپتامبر", "اکتبر", "نوامبر", "دسامبر"
    };

    public static readonly string[] GregorianMonthsEn =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    /// <summary>Index = (int)DayOfWeek, so 0 is Sunday.</summary>
    public static readonly string[] WeekdaysFa =
    {
        "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه"
    };

    public static readonly string[] WeekdaysEn =
    {
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
    };

    public static readonly string[] WeekdaysShortFa = { "ی", "د", "س", "چ", "پ", "ج", "ش" };

    public static readonly string[] WeekdaysShortEn = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };

    /// <summary>The Persian week starts on Saturday; the Gregorian calendar here starts on Monday.</summary>
    public static DayOfWeek FirstDayOfWeek(CalendarKind kind) =>
        kind == CalendarKind.Persian ? DayOfWeek.Saturday : DayOfWeek.Monday;

    public static (int Year, int Month, int Day) ToJalali(DateTime date) =>
        (Persian.GetYear(date), Persian.GetMonth(date), Persian.GetDayOfMonth(date));

    public static DateTime FromJalali(int year, int month, int day) => Persian.ToDateTime(year, month, day, 0, 0, 0, 0);

    public static int DaysInJalaliMonth(int year, int month) => Persian.GetDaysInMonth(year, month);

    public static string MonthName(int month, CalendarKind kind, AppLanguage language)
    {
        var index = Math.Clamp(month, 1, 12) - 1;
        return kind switch
        {
            CalendarKind.Persian => language == AppLanguage.Persian ? JalaliMonthsFa[index] : JalaliMonthsEn[index],
            _ => language == AppLanguage.Persian ? GregorianMonthsFa[index] : GregorianMonthsEn[index]
        };
    }

    public static readonly string[] JalaliMonthsEn =
    {
        "Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar",
        "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand"
    };

    public static string WeekdayName(DayOfWeek day, AppLanguage language) =>
        language == AppLanguage.Persian ? WeekdaysFa[(int)day] : WeekdaysEn[(int)day];

    public static string WeekdayShort(DayOfWeek day, AppLanguage language) =>
        language == AppLanguage.Persian ? WeekdaysShortFa[(int)day] : WeekdaysShortEn[(int)day];

    /// <summary>Year, month and day number in the chosen calendar.</summary>
    public static (int Year, int Month, int Day) Split(DateTime date, CalendarKind kind) =>
        kind == CalendarKind.Persian ? ToJalali(date) : (date.Year, date.Month, date.Day);

    /// <summary>"۱۴ آبان ۱۴۰۵" / "14 Aban 1405" / "5 November 2026". Digits are Western; convert with DigitFormatter.</summary>
    public static string LongDate(DateTime date, CalendarKind kind, AppLanguage language)
    {
        var (y, m, d) = Split(date, kind);
        return $"{d} {MonthName(m, kind, language)} {y}";
    }

    /// <summary>"yyyy/MM/dd" in the chosen calendar (Western digits).</summary>
    public static string NumericDate(DateTime date, CalendarKind kind)
    {
        var (y, m, d) = Split(date, kind);
        return $"{y:0000}/{m:00}/{d:00}";
    }
}
