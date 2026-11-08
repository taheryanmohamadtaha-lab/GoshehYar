using GoshehYar.Core.Clock;
using GoshehYar.Core.Settings;
using Xunit;

namespace GoshehYar.Tests;

public class ClockTests
{
    private static readonly DateTime Afternoon = new(2026, 11, 5, 14, 5, 9);
    private static readonly DateTime Midnight = new(2026, 11, 5, 0, 7, 3);

    [Fact]
    public void Format24HourWithSeconds() =>
        Assert.Equal("14:05:09", ClockFormatter.FormatTime(Afternoon, true, true));

    [Fact]
    public void Format24HourWithoutSeconds() =>
        Assert.Equal("14:05", ClockFormatter.FormatTime(Afternoon, true, false));

    [Fact]
    public void Format12Hour()
    {
        Assert.Equal("2:05:09", ClockFormatter.FormatTime(Afternoon, false, true));
        Assert.Equal("12:07", ClockFormatter.FormatTime(Midnight, false, false));
        Assert.Equal("12:00", ClockFormatter.FormatTime(new DateTime(2026, 1, 1, 12, 0, 0), false, false));
    }

    [Fact]
    public void MeridiemPerLanguage()
    {
        Assert.Equal("PM", ClockFormatter.Meridiem(Afternoon, false, AppLanguage.English));
        Assert.Equal("AM", ClockFormatter.Meridiem(Midnight, false, AppLanguage.English));
        Assert.Equal("ب.ظ", ClockFormatter.Meridiem(Afternoon, false, AppLanguage.Persian));
        Assert.Equal("ق.ظ", ClockFormatter.Meridiem(Midnight, false, AppLanguage.Persian));
        Assert.Equal("", ClockFormatter.Meridiem(Afternoon, true, AppLanguage.English));
    }

    [Fact]
    public void FullFormat()
    {
        Assert.Equal("2:05 PM", ClockFormatter.Format(Afternoon, false, false, AppLanguage.English));
        Assert.Equal("14:05:09", ClockFormatter.Format(Afternoon, true, true, AppLanguage.Persian));
    }

    [Fact]
    public void HandAngles()
    {
        var t = new DateTime(2026, 1, 1, 3, 30, 0);
        Assert.Equal(105.0, ClockFormatter.HourAngle(t), 6);
        Assert.Equal(180.0, ClockFormatter.MinuteAngle(t), 6);
        Assert.Equal(54.0, ClockFormatter.SecondAngle(new DateTime(2026, 1, 1, 0, 0, 9)), 6);
    }

    [Fact]
    public void MillisecondsToNextSecondIsInRange()
    {
        Assert.Equal(1000, ClockFormatter.MillisecondsToNextSecond(new DateTime(2026, 1, 1, 0, 0, 0)));
        Assert.Equal(250, ClockFormatter.MillisecondsToNextSecond(new DateTime(2026, 1, 1, 0, 0, 0, 750)));
    }

    [Fact]
    public void MissingZoneDoesNotThrow()
    {
        Assert.Null(WorldClock.Resolve("No Such Zone XYZ"));
        Assert.Null(WorldClock.Resolve(""));
        Assert.Null(WorldClock.Resolve(null));
        Assert.NotNull(WorldClock.Resolve("UTC"));
    }

    [Fact]
    public void TimeInZoneAndOffsetText()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("test+3:30", TimeSpan.FromHours(3.5), "Test", "Test");
        var utc = new DateTime(2026, 11, 5, 21, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 6, 0, 30, 0), WorldClock.TimeIn(zone, utc));
        Assert.Equal("UTC+3:30", WorldClock.UtcOffsetText(zone, utc));
        Assert.Equal("UTC+0", WorldClock.UtcOffsetText(TimeZoneInfo.Utc, utc));
        var west = TimeZoneInfo.CreateCustomTimeZone("test-5", TimeSpan.FromHours(-5), "West", "West");
        Assert.Equal("UTC-5", WorldClock.UtcOffsetText(west, utc));
    }

    [Fact]
    public void DayOffsetAndDifference()
    {
        var plus3 = TimeZoneInfo.CreateCustomTimeZone("p3", TimeSpan.FromHours(3), "P3", "P3");
        var plus9 = TimeZoneInfo.CreateCustomTimeZone("p9", TimeSpan.FromHours(9), "P9", "P9");
        var minus5 = TimeZoneInfo.CreateCustomTimeZone("m5", TimeSpan.FromHours(-5), "M5", "M5");
        var utc = new DateTime(2026, 11, 5, 20, 0, 0, DateTimeKind.Utc);
        // local +3 -> 23:00 on the 5th; +9 -> 05:00 on the 6th; -5 -> 15:00 on the 5th
        Assert.Equal(1, WorldClock.DayOffset(plus9, plus3, utc));
        Assert.Equal(0, WorldClock.DayOffset(minus5, plus3, utc));
        Assert.Equal(-1, WorldClock.DayOffset(plus3, plus9, utc));
        Assert.Equal("+6", WorldClock.DifferenceText(plus9, plus3, utc));
        Assert.Equal("-8", WorldClock.DifferenceText(minus5, plus3, utc));
        Assert.Equal("0", WorldClock.DifferenceText(plus3, plus3, utc));
    }

    [Fact]
    public void CityNameStripsPrefix()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("x", TimeSpan.FromHours(1), "(UTC+01:00) Paris", "Paris");
        Assert.Equal("Paris", WorldClock.CityName(zone));
        var plain = TimeZoneInfo.CreateCustomTimeZone("y", TimeSpan.Zero, "Plain", "Plain");
        Assert.Equal("Plain", WorldClock.CityName(plain));
        Assert.Equal("Paris (UTC+1)", WorldClock.PickerLabel(zone, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }
}
