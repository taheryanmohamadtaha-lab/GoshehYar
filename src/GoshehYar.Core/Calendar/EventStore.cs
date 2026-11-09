using Microsoft.Data.Sqlite;

namespace GoshehYar.Core.Calendar;

/// <summary>Calendar events in the shared SQLite file. Every call opens and closes its own connection.</summary>
public sealed class EventStore
{
    private readonly string _connectionString;
    private readonly object _gate = new();

    public EventStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();

        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS events (
  id TEXT PRIMARY KEY,
  title TEXT NOT NULL,
  note TEXT NOT NULL,
  event_date TEXT NOT NULL,
  all_day INTEGER NOT NULL,
  time_minutes INTEGER NOT NULL,
  reminder_minutes INTEGER NOT NULL,
  repeat_kind INTEGER NOT NULL,
  created_utc TEXT NOT NULL,
  reminder_utc TEXT NULL,
  reminder_fired INTEGER NOT NULL
);";
            cmd.ExecuteNonQuery();
        }
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    private const string Columns = "id,title,note,event_date,all_day,time_minutes,reminder_minutes,repeat_kind,created_utc,reminder_utc,reminder_fired";

    public IReadOnlyList<CalendarEvent> GetAll()
    {
        lock (_gate)
        {
            var list = new List<CalendarEvent>();
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT " + Columns + " FROM events";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new CalendarEvent
                {
                    Id = r.GetString(0),
                    Title = r.GetString(1),
                    Note = r.GetString(2),
                    Date = ParseDay(r.GetString(3)),
                    AllDay = r.GetInt32(4) != 0,
                    TimeMinutes = r.GetInt32(5),
                    ReminderMinutes = r.GetInt32(6),
                    Repeat = Enum.IsDefined(typeof(EventRepeat), r.GetInt32(7)) ? (EventRepeat)r.GetInt32(7) : EventRepeat.None,
                    CreatedUtc = ParseUtc(r.GetString(8)),
                    ReminderUtc = r.IsDBNull(9) ? null : ParseUtc(r.GetString(9)),
                    ReminderFired = r.GetInt32(10) != 0
                });
            }

            return list;
        }
    }

    public CalendarEvent? GetById(string id) => GetAll().FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Inserts or updates the event. On update the reminder columns are left alone (see <see cref="SaveAndSchedule"/>),
    /// so an old copy never un-announces a reminder.
    /// </summary>
    public void Save(CalendarEvent e)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO events (" + Columns + @")
VALUES ($id,$title,$note,$date,$allday,$time,$rm,$rep,$created,$rem,$fired)
ON CONFLICT(id) DO UPDATE SET
  title=$title, note=$note, event_date=$date, all_day=$allday, time_minutes=$time,
  reminder_minutes=$rm, repeat_kind=$rep;";
            cmd.Parameters.AddWithValue("$id", e.Id);
            cmd.Parameters.AddWithValue("$title", e.Title ?? "");
            cmd.Parameters.AddWithValue("$note", e.Note ?? "");
            cmd.Parameters.AddWithValue("$date", e.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$allday", e.AllDay ? 1 : 0);
            cmd.Parameters.AddWithValue("$time", e.TimeMinutes);
            cmd.Parameters.AddWithValue("$rm", e.ReminderMinutes);
            cmd.Parameters.AddWithValue("$rep", (int)e.Repeat);
            cmd.Parameters.AddWithValue("$created", FormatUtc(e.CreatedUtc));
            cmd.Parameters.AddWithValue("$rem", e.ReminderUtc is null ? DBNull.Value : FormatUtc(e.ReminderUtc.Value));
            cmd.Parameters.AddWithValue("$fired", e.ReminderFired ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Saves the event and sets its next reminder from <paramref name="nowLocal"/> (null when none is due any more).</summary>
    public void SaveAndSchedule(CalendarEvent e, DateTime nowLocal)
    {
        Save(e);
        var next = e.NextReminderLocal(nowLocal);
        e.ReminderUtc = next is null ? null : CalendarEvent.LocalToUtc(next.Value);
        e.ReminderFired = false;
        SetReminder(e.Id, e.ReminderUtc);
    }

    public void Delete(string id)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM events WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Events that occur on the given day, all-day first, then by time and title.</summary>
    public IReadOnlyList<CalendarEvent> GetForDay(DateTime day) => ForDay(GetAll(), day);

    public static IReadOnlyList<CalendarEvent> ForDay(IEnumerable<CalendarEvent> events, DateTime day) =>
        events.Where(e => e.OccursOn(day))
              .OrderBy(e => e.AllDay ? -1 : e.TimeMinutes)
              .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
              .ToList();

    /// <summary>The days in [from, to] that have at least one event.</summary>
    public HashSet<DateTime> GetDaysWithEvents(DateTime from, DateTime to) => DaysWithEvents(GetAll(), from, to);

    public static HashSet<DateTime> DaysWithEvents(IReadOnlyList<CalendarEvent> events, DateTime from, DateTime to)
    {
        var days = new HashSet<DateTime>();
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
        {
            if (events.Any(e => e.OccursOn(d))) days.Add(d);
        }

        return days;
    }

    public IReadOnlyList<CalendarEvent> GetDueReminders(DateTime nowUtc) =>
        GetAll().Where(e => e.ReminderUtc is { } t && !e.ReminderFired && t <= nowUtc).ToList();

    /// <summary>
    /// Marks the reminder as announced. A repeating event then gets the reminder of its next occurrence.
    /// Only reminder columns are written.
    /// </summary>
    public void MarkReminderFired(string id, DateTime nowUtc)
    {
        var e = GetById(id);
        if (e is null) return;

        if (e.Repeat != EventRepeat.None)
        {
            var next = e.NextReminderLocal(nowUtc.ToLocalTime());
            if (next is not null)
            {
                SetReminder(id, CalendarEvent.LocalToUtc(next.Value));
                return;
            }
        }

        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE events SET reminder_fired=1 WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Sets (or clears) the reminder moment and resets the "announced" flag. Also used to snooze.</summary>
    public void SetReminder(string id, DateTime? reminderUtc)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE events SET reminder_utc=$rem, reminder_fired=0 WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$rem", reminderUtc is null ? DBNull.Value : FormatUtc(reminderUtc.Value));
            cmd.ExecuteNonQuery();
        }
    }

    public void Snooze(string id, int minutes, DateTime nowUtc) => SetReminder(id, nowUtc.AddMinutes(minutes));

    private static string FormatUtc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("O");

    private static DateTime ParseUtc(string s) =>
        DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.UtcNow;

    private static DateTime ParseDay(string s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d : DateTime.Today;
}
