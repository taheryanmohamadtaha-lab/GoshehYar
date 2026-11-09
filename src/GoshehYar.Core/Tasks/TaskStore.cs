using System.Globalization;
using Microsoft.Data.Sqlite;

namespace GoshehYar.Core.Tasks;

/// <summary>Tasks in the shared SQLite file (table "tasks"). Every call opens and closes its own connection.</summary>
public sealed class TaskStore
{
    private const string Columns = "id,title,note,priority,category,due,due_has_time,done,completed_utc,created_utc,modified_utc,reminder_offset,reminder_utc,reminder_fired";
    private const string DueFormat = "yyyy-MM-dd'T'HH:mm:ss";

    private readonly string _connectionString;
    private readonly object _gate = new();

    public TaskStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS tasks (
  id TEXT PRIMARY KEY,
  title TEXT NOT NULL,
  note TEXT NOT NULL,
  priority INTEGER NOT NULL,
  category TEXT NOT NULL,
  due TEXT NULL,
  due_has_time INTEGER NOT NULL,
  done INTEGER NOT NULL,
  completed_utc TEXT NULL,
  created_utc TEXT NOT NULL,
  modified_utc TEXT NOT NULL,
  reminder_offset INTEGER NULL,
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

    public IReadOnlyList<TaskItem> GetAll()
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT " + Columns + " FROM tasks";
            return Read(cmd);
        }
    }

    public TaskItem? GetById(string id)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT " + Columns + " FROM tasks WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            return Read(cmd).FirstOrDefault();
        }
    }

    private static List<TaskItem> Read(SqliteCommand cmd)
    {
        var list = new List<TaskItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new TaskItem
            {
                Id = r.GetString(0),
                Title = r.GetString(1),
                Note = r.GetString(2),
                Priority = Enum.IsDefined(typeof(TaskPriority), r.GetInt32(3)) ? (TaskPriority)r.GetInt32(3) : TaskPriority.Normal,
                Category = r.GetString(4),
                Due = r.IsDBNull(5) ? null : ParseDue(r.GetString(5)),
                DueHasTime = r.GetInt32(6) != 0,
                Done = r.GetInt32(7) != 0,
                CompletedUtc = r.IsDBNull(8) ? null : ParseUtc(r.GetString(8)),
                CreatedUtc = ParseUtc(r.GetString(9)),
                ModifiedUtc = ParseUtc(r.GetString(10)),
                ReminderOffsetMinutes = r.IsDBNull(11) ? null : r.GetInt32(11),
                ReminderUtc = r.IsDBNull(12) ? null : ParseUtc(r.GetString(12)),
                ReminderFired = r.GetInt32(13) != 0
            });
        }

        return list;
    }

    /// <summary>
    /// Inserts or updates a task. For an existing row the reminder columns are NOT touched (a snooze or an
    /// "announced" flag written meanwhile must survive); use <see cref="SetReminderSchedule"/> for those.
    /// </summary>
    public void Save(TaskItem t)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO tasks (" + Columns + @")
VALUES ($id,$title,$note,$prio,$cat,$due,$hasTime,$done,$completed,$created,$modified,$off,$rem,$fired)
ON CONFLICT(id) DO UPDATE SET
  title=$title, note=$note, priority=$prio, category=$cat, due=$due, due_has_time=$hasTime, done=$done,
  completed_utc=$completed, created_utc=$created, modified_utc=$modified;";
            cmd.Parameters.AddWithValue("$id", t.Id);
            cmd.Parameters.AddWithValue("$title", t.Title ?? "");
            cmd.Parameters.AddWithValue("$note", t.Note ?? "");
            cmd.Parameters.AddWithValue("$prio", (int)t.Priority);
            cmd.Parameters.AddWithValue("$cat", t.Category ?? "");
            cmd.Parameters.AddWithValue("$due", t.Due is null ? DBNull.Value : t.Due.Value.ToString(DueFormat, CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$hasTime", t.DueHasTime ? 1 : 0);
            cmd.Parameters.AddWithValue("$done", t.Done ? 1 : 0);
            cmd.Parameters.AddWithValue("$completed", t.CompletedUtc is null ? DBNull.Value : FormatUtc(t.CompletedUtc.Value));
            cmd.Parameters.AddWithValue("$created", FormatUtc(t.CreatedUtc));
            cmd.Parameters.AddWithValue("$modified", FormatUtc(t.ModifiedUtc));
            cmd.Parameters.AddWithValue("$off", t.ReminderOffsetMinutes is null ? DBNull.Value : t.ReminderOffsetMinutes.Value);
            cmd.Parameters.AddWithValue("$rem", t.ReminderUtc is null ? DBNull.Value : FormatUtc(t.ReminderUtc.Value));
            cmd.Parameters.AddWithValue("$fired", t.ReminderFired ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }

    public void Delete(string id)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM tasks WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Deletes all finished tasks; returns how many.</summary>
    public int DeleteCompleted()
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM tasks WHERE done=1";
            return cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Open tasks whose reminder time has come and that were not announced yet.</summary>
    public IReadOnlyList<TaskItem> GetDueReminders(DateTime nowUtc)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT " + Columns + " FROM tasks WHERE done=0 AND reminder_fired=0 AND reminder_utc IS NOT NULL";
            return Read(cmd).Where(t => t.ReminderUtc is { } r && r <= nowUtc).ToList();
        }
    }

    /// <summary>Marks only the reminder as announced.</summary>
    public void MarkReminderFired(string id)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE tasks SET reminder_fired=1 WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Sets the reminder moment only (snooze) and resets its announced flag.</summary>
    public void SetReminder(string id, DateTime? reminderUtc)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE tasks SET reminder_utc=$rem, reminder_fired=0 WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$rem", reminderUtc is null ? DBNull.Value : FormatUtc(reminderUtc.Value));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Sets offset and moment of the reminder (after the user changed due date or reminder choice).</summary>
    public void SetReminderSchedule(string id, int? offsetMinutes, DateTime? reminderUtc)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE tasks SET reminder_offset=$off, reminder_utc=$rem, reminder_fired=0 WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$off", offsetMinutes is null ? DBNull.Value : offsetMinutes.Value);
            cmd.Parameters.AddWithValue("$rem", reminderUtc is null ? DBNull.Value : FormatUtc(reminderUtc.Value));
            cmd.ExecuteNonQuery();
        }
    }

    private static string FormatUtc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseUtc(string s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : DateTime.UtcNow;

    private static DateTime? ParseDue(string s) =>
        DateTime.TryParseExact(s, DueFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
}
