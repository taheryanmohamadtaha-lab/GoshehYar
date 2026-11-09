using GoshehYar.Core.Calendar;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Settings;
using Xunit;

namespace GoshehYar.Tests;

public class CalendarTests
{
    [Fact]
    public void Persian_grid_for_Aban_1405_starts_on_saturday()
    {
        // 1 Aban 1405 = 23 October 2026 (Friday).
        var first = DateNames.FromJalali(1405, 8, 1);
        Assert.Equal(new DateTime(2026, 10, 23), first);

        var grid = MonthGrid.Build(1405, 8, CalendarKind.Persian, new DateTime(2026, 11, 9));
        Assert.Equal(42, grid.Count);
        Assert.All(grid.Where((_, i) => i % 7 == 0), c => Assert.Equal(DayOfWeek.Saturday, c.Date.DayOfWeek));

        // Friday is the 7th column, so 1 Aban sits in the last column of the first row.
        Assert.Equal(new DateTime(2026, 10, 17), grid[0].Date);
        Assert.False(grid[5].InMonth);
        Assert.True(grid[6].InMonth);
        Assert.Equal(1, grid[6].DayNumber);
        Assert.True(grid[6].IsFriday);

        Assert.Equal(30, grid.Count(c => c.InMonth));
        Assert.Equal(1, grid.Count(c => c.IsToday));
        Assert.Equal(18, grid.Single(c => c.IsToday).DayNumber); // 18 Aban 1405 = 9 Nov 2026
    }

    [Fact]
    public void Gregorian_grid_for_November_2026_starts_on_monday()
    {
        var grid = MonthGrid.Build(2026, 11, CalendarKind.Gregorian, new DateTime(2026, 11, 9));
        Assert.Equal(42, grid.Count);
        // 1 Nov 2026 is a Sunday, so the first row starts on Monday 26 Oct.
        Assert.Equal(new DateTime(2026, 10, 26), grid[0].Date);
        Assert.All(grid.Where((_, i) => i % 7 == 0), c => Assert.Equal(DayOfWeek.Monday, c.Date.DayOfWeek));
        Assert.False(grid[5].InMonth);
        Assert.True(grid[6].InMonth);
        Assert.Equal(1, grid[6].DayNumber);
        Assert.Equal(30, grid.Count(c => c.InMonth));
        Assert.Equal(9, grid.Single(c => c.IsToday).DayNumber);
    }

    [Fact]
    public void AddMonths_crosses_year_borders()
    {
        Assert.Equal((2027, 1), MonthGrid.AddMonths(2026, 12, 1));
        Assert.Equal((2025, 12), MonthGrid.AddMonths(2026, 1, -1));
        Assert.Equal((1406, 1), MonthGrid.AddMonths(1405, 1, 12));
    }

    [Fact]
    public void Store_round_trip_in_temp_dir()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "notes.db");
        var store = new EventStore(path);

        var ev = new CalendarEvent
        {
            Title = "جلسه",
            Note = "اتاق ۲",
            Date = new DateTime(2026, 11, 12),
            AllDay = false,
            TimeMinutes = 14 * 60 + 30,
            ReminderMinutes = 15,
            Repeat = EventRepeat.None
        };
        store.SaveAndSchedule(ev, new DateTime(2026, 11, 9, 8, 0, 0));

        var loaded = Assert.Single(new EventStore(path).GetAll());
        Assert.Equal(ev.Id, loaded.Id);
        Assert.Equal("جلسه", loaded.Title);
        Assert.Equal("اتاق ۲", loaded.Note);
        Assert.Equal(new DateTime(2026, 11, 12), loaded.Date);
        Assert.False(loaded.AllDay);
        Assert.Equal(870, loaded.TimeMinutes);
        Assert.Equal(15, loaded.ReminderMinutes);
        Assert.NotNull(loaded.ReminderUtc);
        Assert.Equal(CalendarEvent.LocalToUtc(new DateTime(2026, 11, 12, 14, 15, 0)), loaded.ReminderUtc!.Value);

        Assert.Single(store.GetForDay(new DateTime(2026, 11, 12)));
        Assert.Empty(store.GetForDay(new DateTime(2026, 11, 13)));

        loaded.Title = "جلسه تیم";
        store.SaveAndSchedule(loaded, new DateTime(2026, 11, 9, 8, 0, 0));
        Assert.Equal("جلسه تیم", Assert.Single(store.GetAll()).Title);

        store.Delete(ev.Id);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Yearly_repeat_expands_in_both_calendars()
    {
        var gregorian = new CalendarEvent { Date = new DateTime(2020, 11, 5), Repeat = EventRepeat.YearlyGregorian };
        Assert.True(gregorian.OccursOn(new DateTime(2026, 11, 5)));
        Assert.False(gregorian.OccursOn(new DateTime(2026, 11, 6)));
        Assert.False(gregorian.OccursOn(new DateTime(2019, 11, 5)));

        // 14 Aban 1405 = 5 Nov 2026; the same Jalali day in 1406 is 6 Nov 2027.
        var jalali = new CalendarEvent { Date = DateNames.FromJalali(1405, 8, 14), Repeat = EventRepeat.YearlyJalali };
        Assert.True(jalali.OccursOn(DateNames.FromJalali(1406, 8, 14)));
        Assert.True(jalali.OccursOn(DateNames.FromJalali(1410, 8, 14)));
        Assert.False(jalali.OccursOn(DateNames.FromJalali(1406, 8, 15)));
        Assert.False(jalali.OccursOn(DateNames.FromJalali(1404, 8, 14)));

        var single = new CalendarEvent { Date = new DateTime(2026, 11, 5) };
        Assert.True(single.OccursOn(new DateTime(2026, 11, 5)));
        Assert.False(single.OccursOn(new DateTime(2027, 11, 5)));

        Assert.Equal(new DateTime(2027, 11, 5), gregorian.NextOccurrenceOnOrAfter(new DateTime(2026, 11, 6)));
        Assert.Null(single.NextOccurrenceOnOrAfter(new DateTime(2026, 11, 6)));
    }

    [Fact]
    public void Days_with_events_includes_repeats()
    {
        var events = new List<CalendarEvent>
        {
            new() { Date = new DateTime(2026, 11, 3) },
            new() { Date = new DateTime(2020, 11, 20), Repeat = EventRepeat.YearlyGregorian }
        };
        var days = EventStore.DaysWithEvents(events, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));
        Assert.Equal(2, days.Count);
        Assert.Contains(new DateTime(2026, 11, 3), days);
        Assert.Contains(new DateTime(2026, 11, 20), days);
    }

    [Fact]
    public void Due_reminders_are_returned_once_and_snooze_postpones()
    {
        using var dir = new TempDir();
        var store = new EventStore(Path.Combine(dir.Path, "notes.db"));

        var now = new DateTime(2026, 11, 9, 8, 0, 0);
        var ev = new CalendarEvent { Title = "x", Date = new DateTime(2026, 11, 9), AllDay = false, TimeMinutes = 9 * 60, ReminderMinutes = 5 };
        store.SaveAndSchedule(ev, now);

        var remindUtc = CalendarEvent.LocalToUtc(new DateTime(2026, 11, 9, 8, 55, 0));
        Assert.Empty(store.GetDueReminders(remindUtc.AddMinutes(-1)));

        var due = Assert.Single(store.GetDueReminders(remindUtc));
        store.MarkReminderFired(due.Id, remindUtc);
        Assert.Empty(store.GetDueReminders(remindUtc.AddHours(1)));

        store.Snooze(ev.Id, 10, remindUtc);
        Assert.Empty(store.GetDueReminders(remindUtc.AddMinutes(9)));
        Assert.Single(store.GetDueReminders(remindUtc.AddMinutes(10)));
    }

    [Fact]
    public void Repeating_event_gets_next_reminder_after_firing()
    {
        using var dir = new TempDir();
        var store = new EventStore(Path.Combine(dir.Path, "notes.db"));

        var ev = new CalendarEvent { Title = "تولد", Date = new DateTime(2020, 11, 9), AllDay = true, ReminderMinutes = 0, Repeat = EventRepeat.YearlyGregorian };
        store.SaveAndSchedule(ev, new DateTime(2026, 11, 9, 8, 0, 0));

        var first = CalendarEvent.LocalToUtc(new DateTime(2026, 11, 9, 9, 0, 0));
        var due = Assert.Single(store.GetDueReminders(first));
        store.MarkReminderFired(due.Id, first);

        var after = store.GetById(ev.Id)!;
        Assert.False(after.ReminderFired);
        Assert.Equal(CalendarEvent.LocalToUtc(new DateTime(2027, 11, 9, 9, 0, 0)), after.ReminderUtc);
        Assert.Empty(store.GetDueReminders(first.AddDays(1)));
    }

    [Fact]
    public void Event_without_reminder_has_no_due_time()
    {
        var ev = new CalendarEvent { Date = new DateTime(2026, 11, 9), ReminderMinutes = -1 };
        Assert.Null(ev.NextReminderLocal(new DateTime(2026, 11, 1)));

        var dayBefore = new CalendarEvent { Date = new DateTime(2026, 11, 9), AllDay = true, ReminderMinutes = 1440 };
        Assert.Equal(new DateTime(2026, 11, 8, 9, 0, 0), dayBefore.NextReminderLocal(new DateTime(2026, 11, 1)));
        Assert.Null(dayBefore.NextReminderLocal(new DateTime(2026, 11, 8, 10, 0, 0)));
    }

    [Theory]
    [InlineData("1405/08/14", CalendarKind.Persian, 2026, 11, 5)]
    [InlineData("۱۴۰۵-۰۸-۱۴", CalendarKind.Persian, 2026, 11, 5)]
    [InlineData("2026-11-05", CalendarKind.Gregorian, 2026, 11, 5)]
    [InlineData("2026.1.5", CalendarKind.Gregorian, 2026, 1, 5)]
    public void Date_parser_accepts_valid_dates(string text, CalendarKind kind, int y, int m, int d)
    {
        Assert.True(CalendarDateParser.TryParse(text, kind, out var date));
        Assert.Equal(new DateTime(y, m, d), date);
    }

    [Theory]
    [InlineData("", CalendarKind.Persian)]
    [InlineData("abc", CalendarKind.Gregorian)]
    [InlineData("1405/13/01", CalendarKind.Persian)]
    [InlineData("1405/07/31", CalendarKind.Persian)]
    [InlineData("2026/02/30", CalendarKind.Gregorian)]
    [InlineData("2026/02", CalendarKind.Gregorian)]
    public void Date_parser_rejects_invalid_dates(string text, CalendarKind kind)
    {
        Assert.False(CalendarDateParser.TryParse(text, kind, out _));
    }
}
