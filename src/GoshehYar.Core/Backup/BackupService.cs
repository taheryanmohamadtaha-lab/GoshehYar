using System.IO.Compression;
using System.Text.Json;
using GoshehYar.Core.Notes;
using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Backup;

public sealed record BackupInfo(string Path, DateTime CreatedLocal, long SizeBytes);

public sealed class BackupException : Exception
{
    public BackupException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// ZIP backups of the user's data (notes.db, settings.json, playlists.json).
/// Creation is atomic (temp file, then rename). Restore verifies the archive first and keeps a safety copy
/// of the current data, rolling back automatically if anything fails.
/// </summary>
public sealed class BackupService
{
    public const string FilePrefix = "GoshehYar-Backup-";
    private const string NotesEntry = "notes.db";
    private const string SettingsEntry = "settings.json";
    private const string PlaylistsEntry = "playlists.json";
    private const string ManifestEntry = "manifest.json";

    private readonly string _dataDirectory;
    private readonly string _safetyDirectory;
    private readonly Func<NoteStore?> _notes;

    /// <param name="notes">Returns the live note store (used for a consistent database snapshot); may return null.</param>
    public BackupService(string dataDirectory, string safetyDirectory, Func<NoteStore?> notes)
    {
        _dataDirectory = dataDirectory;
        _safetyDirectory = safetyDirectory;
        _notes = notes;
    }

    // ------------------------------------------------------------------ create

    public string CreateBackup(string folder, string appVersion = "")
    {
        Directory.CreateDirectory(folder);
        var name = FilePrefix + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip";
        var target = UniquePath(Path.Combine(folder, name));
        WriteArchive(target, appVersion);
        return target;
    }

    /// <summary>Same content as a backup, written to any file the user picked (ZIP export).</summary>
    public void ExportZip(string targetPath, string appVersion = "")
    {
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        WriteArchive(targetPath, appVersion);
    }

    private void WriteArchive(string targetPath, string appVersion)
    {
        var temp = targetPath + ".tmp";
        var snapshot = Path.Combine(Path.GetTempPath(), "goshehyar-snap-" + Guid.NewGuid().ToString("N") + ".db");

        try
        {
            if (File.Exists(temp)) File.Delete(temp);

            string? notesFile = null;
            var store = _notes();
            if (store is not null)
            {
                store.SnapshotTo(snapshot);
                notesFile = snapshot;
            }
            else if (File.Exists(Path.Combine(_dataDirectory, NotesEntry)))
            {
                notesFile = Path.Combine(_dataDirectory, NotesEntry);
            }

            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var included = new List<string>();
                if (notesFile is not null) { AddFile(zip, notesFile, NotesEntry); included.Add(NotesEntry); }

                var settings = Path.Combine(_dataDirectory, SettingsEntry);
                if (File.Exists(settings)) { AddFile(zip, settings, SettingsEntry); included.Add(SettingsEntry); }

                var playlists = Path.Combine(_dataDirectory, PlaylistsEntry);
                if (File.Exists(playlists)) { AddFile(zip, playlists, PlaylistsEntry); included.Add(PlaylistsEntry); }

                var manifest = JsonSerializer.Serialize(new
                {
                    app = "GoshehYar",
                    version = appVersion,
                    createdUtc = DateTime.UtcNow.ToString("O"),
                    files = included
                });
                var entry = zip.CreateEntry(ManifestEntry);
                using var w = new StreamWriter(entry.Open());
                w.Write(manifest);
            }

            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(temp, targetPath);
        }
        catch (Exception ex)
        {
            TryDelete(temp);
            throw new BackupException("Backup failed: " + ex.Message, ex);
        }
        finally
        {
            TryDelete(snapshot);
        }
    }

    private static void AddFile(ZipArchive zip, string path, string entryName)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = entry.Open();
        input.CopyTo(output);
    }

    // -------------------------------------------------------------- list/prune

    public IReadOnlyList<BackupInfo> List(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<BackupInfo>();

        return new DirectoryInfo(folder)
            .GetFiles(FilePrefix + "*.zip")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new BackupInfo(f.FullName, f.LastWriteTime, f.Length))
            .ToList();
    }

    /// <summary>Deletes the oldest backups beyond <paramref name="keep"/>. Only files created by this app are touched.</summary>
    public int Prune(string folder, int keep)
    {
        keep = Math.Max(1, keep);
        var removed = 0;
        foreach (var b in List(folder).Skip(keep))
        {
            if (TryDelete(b.Path)) removed++;
        }

        return removed;
    }

    public static bool IsDue(DateTime? lastBackupUtc, BackupFrequency frequency, DateTime nowUtc)
    {
        if (lastBackupUtc is null) return true;
        var interval = frequency == BackupFrequency.Weekly ? TimeSpan.FromDays(7) : TimeSpan.FromDays(1);
        return nowUtc - lastBackupUtc.Value >= interval;
    }

    // ------------------------------------------------------------------ verify

    /// <summary>Checks that the archive is a readable GoshehYar backup with a healthy database.</summary>
    public bool Verify(string zipPath, out string error)
    {
        error = "";
        var temp = Path.Combine(Path.GetTempPath(), "goshehyar-verify-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            using var zip = ZipFile.OpenRead(zipPath);

            if (zip.GetEntry(ManifestEntry) is null) { error = "manifest missing"; return false; }

            var notes = zip.GetEntry(NotesEntry);
            if (notes is null) { error = "notes database missing"; return false; }

            var dbPath = Path.Combine(temp, NotesEntry);
            notes.ExtractToFile(dbPath, overwrite: true);
            if (!NoteStore.IsHealthy(dbPath)) { error = "notes database is damaged"; return false; }

            foreach (var name in new[] { SettingsEntry, PlaylistsEntry })
            {
                var e = zip.GetEntry(name);
                if (e is null) continue;
                var p = Path.Combine(temp, name);
                e.ExtractToFile(p, overwrite: true);
                try { JsonDocument.Parse(File.ReadAllText(p)).Dispose(); }
                catch (JsonException) { error = name + " is damaged"; return false; }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch (Exception) { }
        }
    }

    // ----------------------------------------------------------------- restore

    /// <summary>
    /// Replaces the current data with the archive's content. The current data is copied to a safety folder first
    /// and put back if anything goes wrong. Returns the safety folder.
    /// The caller must stop using the database before and restart the app after.
    /// </summary>
    public string Restore(string zipPath)
    {
        if (!Verify(zipPath, out var error))
        {
            throw new BackupException("The backup file is not valid: " + error);
        }

        Directory.CreateDirectory(_dataDirectory);
        var safety = Path.Combine(_safetyDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(safety);

        var names = new[] { NotesEntry, SettingsEntry, PlaylistsEntry };
        foreach (var n in names)
        {
            var src = Path.Combine(_dataDirectory, n);
            if (File.Exists(src)) File.Copy(src, Path.Combine(safety, n), overwrite: true);
        }

        var staging = Path.Combine(_dataDirectory, "restore-staging-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var n in names)
                {
                    zip.GetEntry(n)?.ExtractToFile(Path.Combine(staging, n), overwrite: true);
                }
            }

            foreach (var n in names)
            {
                var staged = Path.Combine(staging, n);
                var live = Path.Combine(_dataDirectory, n);
                if (File.Exists(staged))
                {
                    File.Copy(staged, live, overwrite: true);
                }
            }

            // Drop SQLite side files that belong to the old database.
            TryDelete(Path.Combine(_dataDirectory, NotesEntry + "-wal"));
            TryDelete(Path.Combine(_dataDirectory, NotesEntry + "-shm"));
            TryDelete(Path.Combine(_dataDirectory, NotesEntry + "-journal"));

            return safety;
        }
        catch (Exception ex)
        {
            // Roll back from the safety copy.
            foreach (var n in names)
            {
                var saved = Path.Combine(safety, n);
                if (File.Exists(saved))
                {
                    try { File.Copy(saved, Path.Combine(_dataDirectory, n), overwrite: true); } catch (Exception) { }
                }
            }

            throw new BackupException("Restore failed; your previous data was put back. " + ex.Message, ex);
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); } catch (Exception) { }
        }
    }

    // ---------------------------------------------------------------- helpers

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}-{i}.zip");
            if (!File.Exists(candidate)) return candidate;
        }

        return path;
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
