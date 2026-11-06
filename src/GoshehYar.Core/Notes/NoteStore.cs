using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace GoshehYar.Core.Notes;

/// <summary>Notes saved in a local SQLite file. Every call opens and closes its own connection (no file locks are kept).</summary>
public sealed class NoteStore
{
    private readonly string _connectionString;
    private readonly object _gate = new();

    public NoteStore(string databasePath)
    {
        DatabasePath = databasePath;
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();

        Initialize();
    }

    public string DatabasePath { get; }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    private void Initialize()
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS notes (
  id TEXT PRIMARY KEY,
  title TEXT NOT NULL,
  body TEXT NOT NULL,
  is_checklist INTEGER NOT NULL,
  items TEXT NOT NULL,
  category TEXT NOT NULL,
  color TEXT NOT NULL,
  pinned INTEGER NOT NULL,
  created_utc TEXT NOT NULL,
  modified_utc TEXT NOT NULL,
  reminder_utc TEXT NULL,
  reminder_fired INTEGER NOT NULL
);";
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<Note> GetAll()
    {
        lock (_gate)
        {
            var list = new List<Note>();
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id,title,body,is_checklist,items,category,color,pinned,created_utc,modified_utc,reminder_utc,reminder_fired FROM notes";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var note = new Note
                {
                    Id = r.GetString(0),
                    Title = r.GetString(1),
                    Body = r.GetString(2),
                    IsChecklist = r.GetInt32(3) != 0,
                    Category = r.GetString(5),
                    Color = r.GetString(6),
                    Pinned = r.GetInt32(7) != 0,
                    CreatedUtc = ParseDate(r.GetString(8)),
                    ModifiedUtc = ParseDate(r.GetString(9)),
                    ReminderUtc = r.IsDBNull(10) ? null : ParseDate(r.GetString(10)),
                    ReminderFired = r.GetInt32(11) != 0
                };
                try
                {
                    note.Items = JsonSerializer.Deserialize<List<ChecklistItem>>(r.GetString(4)) ?? new();
                }
                catch (JsonException)
                {
                    note.Items = new();
                }

                list.Add(note);
            }

            return list;
        }
    }

    /// <summary>Inserts or updates the note. Does not change ModifiedUtc; the caller decides.</summary>
    public void Save(Note note)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO notes (id,title,body,is_checklist,items,category,color,pinned,created_utc,modified_utc,reminder_utc,reminder_fired)
VALUES ($id,$title,$body,$cl,$items,$cat,$color,$pin,$created,$modified,$rem,$fired)
ON CONFLICT(id) DO UPDATE SET
  title=$title, body=$body, is_checklist=$cl, items=$items, category=$cat, color=$color, pinned=$pin,
  created_utc=$created, modified_utc=$modified, reminder_utc=$rem, reminder_fired=$fired;";
            cmd.Parameters.AddWithValue("$id", note.Id);
            cmd.Parameters.AddWithValue("$title", note.Title ?? "");
            cmd.Parameters.AddWithValue("$body", note.Body ?? "");
            cmd.Parameters.AddWithValue("$cl", note.IsChecklist ? 1 : 0);
            cmd.Parameters.AddWithValue("$items", JsonSerializer.Serialize(note.Items ?? new List<ChecklistItem>()));
            cmd.Parameters.AddWithValue("$cat", note.Category ?? "");
            cmd.Parameters.AddWithValue("$color", note.Color ?? "");
            cmd.Parameters.AddWithValue("$pin", note.Pinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$created", FormatDate(note.CreatedUtc));
            cmd.Parameters.AddWithValue("$modified", FormatDate(note.ModifiedUtc));
            cmd.Parameters.AddWithValue("$rem", note.ReminderUtc is null ? DBNull.Value : FormatDate(note.ReminderUtc.Value));
            cmd.Parameters.AddWithValue("$fired", note.ReminderFired ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }

    public void Delete(string id)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM notes WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Notes whose reminder time has come and that were not announced yet.</summary>
    public IReadOnlyList<Note> GetDueReminders(DateTime nowUtc) =>
        GetAll().Where(n => n.ReminderUtc is { } t && !n.ReminderFired && t <= nowUtc).ToList();

    /// <summary>Writes a consistent copy of the database to <paramref name="destinationPath"/> (must not exist).</summary>
    public void SnapshotTo(string destinationPath)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "VACUUM INTO $path";
            cmd.Parameters.AddWithValue("$path", destinationPath);
            cmd.ExecuteNonQuery();
        }
    }

    public static bool IsHealthy(string databasePath)
    {
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            using var c = new SqliteConnection(cs);
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            var result = cmd.ExecuteScalar() as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) return false;
            cmd.CommandText = "SELECT count(*) FROM notes";
            cmd.ExecuteScalar();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string FormatDate(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("O");

    private static DateTime ParseDate(string s) =>
        DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
            ? d.ToUniversalTime()
            : DateTime.UtcNow;
}
