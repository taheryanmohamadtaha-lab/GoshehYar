using System.Globalization;
using Microsoft.Data.Sqlite;

namespace GoshehYar.Core.Clipboard;

public sealed class ClipEntry
{
    public long Id { get; set; }
    public string Text { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public bool Pinned { get; set; }
}

/// <summary>Rules for what may enter the history.</summary>
public static class ClipboardPolicy
{
    public const int MaxChars = 20000;

    /// <summary>Only non-empty text of reasonable size is kept.</summary>
    public static bool ShouldStore(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= MaxChars;
}

/// <summary>
/// Clipboard text history in its own SQLite file (never part of the backups, because a clipboard may hold secrets).
/// Pinned items are never trimmed automatically.
/// </summary>
public sealed class ClipboardStore
{
    private readonly string _connectionString;
    private readonly object _gate = new();

    public ClipboardStore(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();

        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS clips (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  text TEXT NOT NULL,
  created_utc TEXT NOT NULL,
  pinned INTEGER NOT NULL
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

    /// <summary>
    /// Adds a copied text. The same text again moves to the top (keeping its pin). Afterwards only the newest
    /// <paramref name="maxItems"/> unpinned items stay. Returns false when the text was refused.
    /// </summary>
    public bool Add(string text, int maxItems, DateTime? nowUtc = null)
    {
        if (!ClipboardPolicy.ShouldStore(text)) return false;
        var now = nowUtc ?? DateTime.UtcNow;

        lock (_gate)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();

            var pinned = 0;
            using (var find = c.CreateCommand())
            {
                find.Transaction = tx;
                find.CommandText = "SELECT pinned FROM clips WHERE text=$t";
                find.Parameters.AddWithValue("$t", text);
                using var r = find.ExecuteReader();
                while (r.Read()) pinned = Math.Max(pinned, r.GetInt32(0));
            }

            using (var del = c.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM clips WHERE text=$t";
                del.Parameters.AddWithValue("$t", text);
                del.ExecuteNonQuery();
            }

            using (var ins = c.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO clips (text, created_utc, pinned) VALUES ($t, $c, $p)";
                ins.Parameters.AddWithValue("$t", text);
                ins.Parameters.AddWithValue("$c", now.ToString("O", CultureInfo.InvariantCulture));
                ins.Parameters.AddWithValue("$p", pinned);
                ins.ExecuteNonQuery();
            }

            using (var trim = c.CreateCommand())
            {
                trim.Transaction = tx;
                trim.CommandText = @"DELETE FROM clips WHERE pinned=0 AND id NOT IN
                                     (SELECT id FROM clips WHERE pinned=0 ORDER BY id DESC LIMIT $n)";
                trim.Parameters.AddWithValue("$n", Math.Max(1, maxItems));
                trim.ExecuteNonQuery();
            }

            tx.Commit();
        }

        return true;
    }

    /// <summary>Pinned first, then newest first. Optional case-insensitive search.</summary>
    public IReadOnlyList<ClipEntry> GetAll(string? search = null)
    {
        var list = new List<ClipEntry>();
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id, text, created_utc, pinned FROM clips ORDER BY pinned DESC, id DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ClipEntry
                {
                    Id = r.GetInt64(0),
                    Text = r.GetString(1),
                    CreatedUtc = DateTime.TryParse(r.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
                        ? d.ToUniversalTime() : DateTime.UtcNow,
                    Pinned = r.GetInt32(3) != 0
                });
            }
        }

        var q = (search ?? "").Trim();
        return q.Length == 0
            ? list
            : list.Where(e => e.Text.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
    }

    public void SetPinned(long id, bool pinned) => Execute("UPDATE clips SET pinned=$p WHERE id=$id", ("$p", pinned ? 1 : 0), ("$id", id));

    public void Delete(long id) => Execute("DELETE FROM clips WHERE id=$id", ("$id", id));

    /// <summary>Removes everything, or everything except pinned items.</summary>
    public void Clear(bool keepPinned) =>
        Execute(keepPinned ? "DELETE FROM clips WHERE pinned=0" : "DELETE FROM clips");

    private void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        lock (_gate)
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);
            cmd.ExecuteNonQuery();
        }
    }
}
