using GoshehYar.Core.Dates;
using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Calendar;

/// <summary>One cell of the month grid.</summary>
public sealed record GridDay(DateTime Date, int DayNumber, bool InMonth, bool IsToday, bool IsFriday);

/// <summary>Builds the 6 x 7 grid of a month in the Persian (Saturday first) or Gregorian (Monday first) calendar.</summary>
public static class MonthGrid
{
    public const int Rows = 6;
    public const int Columns = 7;

    public static DateTime FirstOfMonth(int year, int month, CalendarKind kind) =>
        kind == CalendarKind.Persian ? DateNames.FromJalali(year, month, 1) : new DateTime(year, month, 1);

    public static int DaysInMonth(int year, int month, CalendarKind kind) =>
        kind == CalendarKind.Persian ? DateNames.DaysInJalaliMonth(year, month) : DateTime.DaysInMonth(year, month);

    /// <summary>Moves a (year, month) pair by a number of months, which may be negative.</summary>
    public static (int Year, int Month) AddMonths(int year, int month, int delta)
    {
        var index = year * 12 + (month - 1) + delta;
        return (index / 12, index % 12 + 1);
    }

    /// <summary>The 42 days shown for the month; cells outside the month belong to the neighbouring months.</summary>
    public static IReadOnlyList<GridDay> Build(int year, int month, CalendarKind kind, DateTime today)
    {
        var first = FirstOfMonth(year, month, kind);
        var length = DaysInMonth(year, month, kind);
        var weekStart = DateNames.FirstDayOfWeek(kind);
        var offset = ((int)first.DayOfWeek - (int)weekStart + 7) % 7;
        var start = first.AddDays(-offset);

        var cells = new List<GridDay>(Rows * Columns);
        for (var i = 0; i < Rows * Columns; i++)
        {
            var date = start.AddDays(i);
            var inMonth = i >= offset && i < offset + length;
            var (_, _, day) = DateNames.Split(date, kind);
            cells.Add(new GridDay(date, day, inMonth, date.Date == today.Date, date.DayOfWeek == DayOfWeek.Friday));
        }

        return cells;
    }
}
