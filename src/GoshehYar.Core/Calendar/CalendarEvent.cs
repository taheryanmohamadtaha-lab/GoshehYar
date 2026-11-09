using GoshehYar.Core.Dates;

namespace GoshehYar.Core.Calendar;

/// <summary>How an event repeats. A yearly repeat keeps the same month and day number of the chosen calendar.</summary>
public enum EventRepeat
{
    None = 0,
    YearlyGregorian = 1,
    YearlyJalali = 2
}

/// <summary>One calendar event or occasion. Dates and times are local (no time zone).</summary>
public sealed class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "";

    public string Note { get; set; } = "";

    /// <summary>The day of the event (the first day, for repeating events). Time part is ignored.</summary>
    public DateTime Date { get; set; } = DateTime.Today;

    public bool AllDay { get; set; } = true;

    /// <summary>Start time as minutes after midnight (0..1439); used when <see cref="AllDay"/> is false.</summary>
    public int TimeMinutes { get; set; } = 9 * 60;

    /// <summary>-1 = no reminder, 0 = at the time, otherwise minutes before (1440 = one day).</summary>
    public int ReminderMinutes { get; set; } = -1;

    public EventRepeat Repeat { get; set; } = EventRepeat.None;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the next reminder is due (UTC) and whether it was announced already.</summary>
    public DateTime? ReminderUtc { get; set; }

    public bool ReminderFired { get; set; }

    /// <summary>Reminders of all-day events come at this time of day.</summary>
    public const int AllDayReminderMinutes = 9 * 60;

    private int TimeOfDayMinutes => AllDay ? AllDayReminderMinutes : Math.Clamp(TimeMinutes, 0, 1439);

    /// <summary>True when the event takes place on the given day (repeats included).</summary>
    public bool OccursOn(DateTime day)
    {
        var d = day.Date;
        var start = Date.Date;
        if (d < start) return false;

        switch (Repeat)
        {
            case EventRepeat.YearlyGregorian:
                return d.Month == start.Month && (d.Day == start.Day || (start.Month == 2 && start.Day == 29 && d.Month == 2 && d.Day == 28 && !DateTime.IsLeapYear(d.Year)));
            case EventRepeat.YearlyJalali:
                {
                    var (_, sm, sd) = DateNames.ToJalali(start);
                    var (jy, jm, jd) = DateNames.ToJalali(d);
                    if (jm != sm) return false;
                    // Day 30 of Esfand exists only in leap years; in other years it falls on day 29.
                    var effective = Math.Min(sd, DateNames.DaysInJalaliMonth(jy, sm));
                    return jd == effective;
                }
            default:
                return d == start;
        }
    }

    /// <summary>First day on or after <paramref name="from"/> on which the event occurs, or null if it never does again.</summary>
    public DateTime? NextOccurrenceOnOrAfter(DateTime from)
    {
        var f = from.Date;
        if (Repeat == EventRepeat.None) return Date.Date >= f ? Date.Date : null;

        var begin = f < Date.Date ? Date.Date : f;
        // A yearly event always has an occurrence within the next 400 days.
        for (var i = 0; i <= 400; i++)
        {
            var d = begin.AddDays(i);
            if (OccursOn(d)) return d;
        }

        return null;
    }

    /// <summary>
    /// The next reminder moment (local time) that lies after <paramref name="afterLocal"/>; null when there is
    /// no reminder or all moments have passed.
    /// </summary>
    public DateTime? NextReminderLocal(DateTime afterLocal)
    {
        if (ReminderMinutes < 0) return null;

        var tod = TimeSpan.FromMinutes(TimeOfDayMinutes);
        var offset = TimeSpan.FromMinutes(ReminderMinutes);

        // A reminder can come up to a day (or more) before the event, so start searching that much earlier.
        var cursor = afterLocal.Date.AddDays(-1 - (int)Math.Ceiling(offset.TotalDays));
        for (var guard = 0; guard < 6; guard++)
        {
            var occurrence = NextOccurrenceOnOrAfter(cursor);
            if (occurrence is null) return null;

            var remind = occurrence.Value + tod - offset;
            if (remind > afterLocal) return remind;
            cursor = occurrence.Value.AddDays(1);
        }

        return null;
    }

    public static DateTime LocalToUtc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
}
