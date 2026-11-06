using GoshehYar.Core.Backup;
using GoshehYar.Core.Music;
using GoshehYar.Core.Notes;
using GoshehYar.Core.Settings;

namespace GoshehYar.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gy-test-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch (IOException) { }
    }
}

public class NoteStoreTests
{
    [Fact]
    public void Notes_survive_reopening_the_database()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "notes.db");

        var store = new NoteStore(path);
        var note = new Note { Title = "سلام", Body = "متن آزمایشی", Category = "کار", Pinned = true };
        note.Items.Add(new ChecklistItem { Text = "a", Done = true });
        store.Save(note);

        var reopened = new NoteStore(path).GetAll();
        var loaded = Assert.Single(reopened);
        Assert.Equal("سلام", loaded.Title);
        Assert.Equal("متن آزمایشی", loaded.Body);
        Assert.True(loaded.Pinned);
        Assert.True(loaded.Items[0].Done);
    }

    [Fact]
    public void Save_updates_and_delete_removes()
    {
        using var dir = new TempDir();
        var store = new NoteStore(Path.Combine(dir.Path, "n.db"));
        var n = new Note { Title = "one" };
        store.Save(store.GetAll().Count == 0 ? n : n);
        n.Title = "two";
        store.Save(n);
        Assert.Equal("two", Assert.Single(store.GetAll()).Title);
        store.Delete(n.Id);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Due_reminders_are_found_once()
    {
        using var dir = new TempDir();
        var store = new NoteStore(Path.Combine(dir.Path, "n.db"));
        var now = DateTime.UtcNow;
        store.Save(new Note { Title = "past", ReminderUtc = now.AddMinutes(-1) });
        store.Save(new Note { Title = "future", ReminderUtc = now.AddHours(1) });
        store.Save(new Note { Title = "done", ReminderUtc = now.AddMinutes(-5), ReminderFired = true });

        var due = store.GetDueReminders(now);
        Assert.Equal("past", Assert.Single(due).Title);
    }
}

public class NoteToolsTests
{
    private static List<Note> Sample() => new()
    {
        new Note { Title = "Shopping", Body = "milk", Category = "home", ModifiedUtc = DateTime.UtcNow.AddHours(-2) },
        new Note { Title = "Work", Body = "report", Category = "job", Pinned = true, ModifiedUtc = DateTime.UtcNow.AddHours(-5) },
        new Note { Title = "Ideas", Body = "app", Category = "job", ModifiedUtc = DateTime.UtcNow },
    };

    [Fact]
    public void Pinned_notes_come_first_then_newest()
    {
        var titles = NoteTools.Filter(Sample(), null, null).Select(n => n.Title);
        Assert.Equal(new[] { "Work", "Ideas", "Shopping" }, titles);
    }

    [Fact]
    public void Search_and_category_filter()
    {
        Assert.Equal("Shopping", Assert.Single(NoteTools.Filter(Sample(), "MILK", null)).Title);
        Assert.Equal(2, NoteTools.Filter(Sample(), null, "job").Count);
        Assert.Equal(new[] { "home", "job" }, NoteTools.Categories(Sample()));
    }

    [Fact]
    public void Checklist_export_formats()
    {
        var n = new Note { Title = "T", IsChecklist = true };
        n.Items.Add(new ChecklistItem { Text = "a", Done = true });
        n.Items.Add(new ChecklistItem { Text = "b" });
        Assert.Contains("- [x] a", NoteTools.ToMarkdown(n));
        Assert.Contains("- [ ] b", NoteTools.ToMarkdown(n));
        Assert.Contains("[x] a", NoteTools.ToText(n));
    }

    [Fact]
    public void File_names_are_made_safe()
    {
        Assert.Equal("ab", NoteTools.SafeFileName("a/b:"));
        Assert.Equal("note", NoteTools.SafeFileName("  "));
    }
}

public class BackupServiceTests
{
    private static (BackupService service, NoteStore store, string data, string backups) Setup(TempDir dir)
    {
        var data = Path.Combine(dir.Path, "data");
        Directory.CreateDirectory(data);
        var store = new NoteStore(Path.Combine(data, "notes.db"));
        File.WriteAllText(Path.Combine(data, "settings.json"), "{\"Language\":\"Persian\"}");
        var service = new BackupService(data, Path.Combine(dir.Path, "safety"), () => store);
        return (service, store, data, Path.Combine(dir.Path, "backups"));
    }

    [Fact]
    public void Backup_can_be_verified_and_restored()
    {
        using var dir = new TempDir();
        var (service, store, data, backups) = Setup(dir);

        store.Save(new Note { Title = "keep me" });
        var zip = service.CreateBackup(backups, "1.0");
        Assert.True(service.Verify(zip, out var error), error);

        // Change data after the backup, then restore.
        store.Save(new Note { Title = "added later" });
        File.WriteAllText(Path.Combine(data, "settings.json"), "{\"Language\":\"English\"}");

        var safety = service.Restore(zip);

        var restored = new NoteStore(Path.Combine(data, "notes.db")).GetAll();
        Assert.Equal("keep me", Assert.Single(restored).Title);
        Assert.Contains("Persian", File.ReadAllText(Path.Combine(data, "settings.json")));

        // The state before the restore is kept in the safety folder.
        Assert.Equal(2, new NoteStore(Path.Combine(safety, "notes.db")).GetAll().Count);
    }

    [Fact]
    public void Corrupt_archive_is_rejected_and_data_stays_untouched()
    {
        using var dir = new TempDir();
        var (service, store, data, backups) = Setup(dir);
        store.Save(new Note { Title = "safe" });

        Directory.CreateDirectory(backups);
        var bad = Path.Combine(backups, "bad.zip");
        File.WriteAllText(bad, "this is not a zip");

        Assert.False(service.Verify(bad, out _));
        Assert.Throws<BackupException>(() => service.Restore(bad));
        Assert.Equal("safe", Assert.Single(new NoteStore(Path.Combine(data, "notes.db")).GetAll()).Title);
    }

    [Fact]
    public void Prune_keeps_only_the_newest_backups()
    {
        using var dir = new TempDir();
        var (service, _, _, backups) = Setup(dir);
        Directory.CreateDirectory(backups);
        for (var i = 0; i < 5; i++)
        {
            var p = Path.Combine(backups, $"{BackupService.FilePrefix}2026010{i}-000000.zip");
            File.WriteAllText(p, "x");
            File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddDays(-10 + i));
        }

        File.WriteAllText(Path.Combine(backups, "unrelated.zip"), "x");

        Assert.Equal(3, service.Prune(backups, 2));
        Assert.Equal(2, service.List(backups).Count);
        Assert.True(File.Exists(Path.Combine(backups, "unrelated.zip")));
    }

    [Fact]
    public void Due_calculation()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(BackupService.IsDue(null, BackupFrequency.Daily, now));
        Assert.False(BackupService.IsDue(now.AddHours(-3), BackupFrequency.Daily, now));
        Assert.True(BackupService.IsDue(now.AddHours(-25), BackupFrequency.Daily, now));
        Assert.False(BackupService.IsDue(now.AddDays(-3), BackupFrequency.Weekly, now));
        Assert.True(BackupService.IsDue(now.AddDays(-8), BackupFrequency.Weekly, now));
    }
}

public class PlaybackQueueTests
{
    [Fact]
    public void Sequential_play_wraps_with_repeat_all()
    {
        var q = new PlaybackQueue(new Random(1)) { Repeat = RepeatMode.All };
        q.Reset(3, shuffle: false);
        Assert.Equal(0, q.Next(false));
        Assert.Equal(1, q.Next(true));
        Assert.Equal(2, q.Next(true));
        Assert.Equal(0, q.Next(true));
        Assert.Equal(2, q.Previous());
    }

    [Fact]
    public void Repeat_none_stops_at_the_end_but_manual_next_wraps()
    {
        var q = new PlaybackQueue { Repeat = RepeatMode.None };
        q.Reset(2, false);
        q.Next(false);
        Assert.Equal(1, q.Next(true));
        Assert.Equal(-1, q.Next(true));
        Assert.Equal(0, q.Next(false));
    }

    [Fact]
    public void Repeat_one_replays_the_same_track_when_it_ends()
    {
        var q = new PlaybackQueue { Repeat = RepeatMode.One };
        q.Reset(3, false);
        q.Next(false);
        q.Next(false);
        Assert.Equal(1, q.Next(true));
        Assert.Equal(2, q.Next(false));
    }

    [Fact]
    public void Shuffle_plays_every_track_once_per_round()
    {
        var q = new PlaybackQueue(new Random(7)) { Repeat = RepeatMode.All };
        q.Reset(6, shuffle: true);
        var seen = Enumerable.Range(0, 6).Select(_ => q.Next(true)).OrderBy(x => x).ToList();
        Assert.Equal(Enumerable.Range(0, 6), seen);
    }

    [Fact]
    public void Select_index_continues_from_there()
    {
        var q = new PlaybackQueue();
        q.Reset(4, false);
        q.SelectIndex(2);
        Assert.Equal(2, q.CurrentIndex);
        Assert.Equal(3, q.Next(true));
    }

    [Fact]
    public void Empty_queue_returns_minus_one()
    {
        var q = new PlaybackQueue();
        q.Reset(0, false);
        Assert.Equal(-1, q.Next(true));
        Assert.Equal(-1, q.Previous());
    }
}

public class Phase2SettingsTests
{
    [Fact]
    public void New_settings_are_clamped()
    {
        var s = new AppSettings { ScreenshotDelaySeconds = 99, MusicVolume = 5, BackupKeepCount = 0 };
        s.Normalize();
        Assert.Equal(30, s.ScreenshotDelaySeconds);
        Assert.Equal(1.0, s.MusicVolume);
        Assert.Equal(1, s.BackupKeepCount);
    }
}
