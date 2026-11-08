using GoshehYar.Core.Dates;
using GoshehYar.Core.Settings;

namespace GoshehYar.Tests;

public class DateNamesTests
{
    [Fact]
    public void Nowruz_1405_is_21_march_2026()
    {
        var (y, m, d) = DateNames.ToJalali(new DateTime(2026, 3, 21));
        Assert.Equal(1405, y);
        Assert.Equal(1, m);
        Assert.Equal(1, d);
    }

    [Fact]
    public void Jalali_round_trip()
    {
        var date = new DateTime(2026, 11, 5);
        var (y, m, d) = DateNames.ToJalali(date);
        Assert.Equal(date, DateNames.FromJalali(y, m, d));
    }

    [Fact]
    public void Names_follow_language_and_calendar()
    {
        Assert.Equal("آبان", DateNames.MonthName(8, CalendarKind.Persian, AppLanguage.Persian));
        Assert.Equal("Aban", DateNames.MonthName(8, CalendarKind.Persian, AppLanguage.English));
        Assert.Equal("November", DateNames.MonthName(11, CalendarKind.Gregorian, AppLanguage.English));
        Assert.Equal("پنجشنبه", DateNames.WeekdayName(DayOfWeek.Thursday, AppLanguage.Persian));
    }

    [Fact]
    public void Long_date_uses_chosen_calendar()
    {
        Assert.Equal("5 November 2026", DateNames.LongDate(new DateTime(2026, 11, 5), CalendarKind.Gregorian, AppLanguage.English));
        Assert.Equal("14 آبان 1405", DateNames.LongDate(new DateTime(2026, 11, 5), CalendarKind.Persian, AppLanguage.Persian));
    }
}
