using GoshehYar.Core.Notes;

namespace GoshehYar.Tests;

public class ReminderStoreTests
{
    [Fact]
    public void Marking_a_reminder_fired_does_not_touch_the_text()
    {
        using var dir = new TempDir();
        var store = new NoteStore(Path.Combine(dir.Path, "notes.db"));
        var note = new Note { Title = "t", Body = "new text", ReminderUtc = DateTime.UtcNow.AddMinutes(-1) };
        store.Save(note);

        store.MarkReminderFired(note.Id);

        var loaded = Assert.Single(store.GetAll());
        Assert.Equal("new text", loaded.Body);
        Assert.True(loaded.ReminderFired);
        Assert.Empty(store.GetDueReminders(DateTime.UtcNow));
    }

    [Fact]
    public void Snoozing_makes_the_note_due_again_later()
    {
        using var dir = new TempDir();
        var store = new NoteStore(Path.Combine(dir.Path, "notes.db"));
        var note = new Note { Title = "t", ReminderUtc = DateTime.UtcNow.AddMinutes(-1), ReminderFired = true };
        store.Save(note);

        var later = DateTime.UtcNow.AddMinutes(10);
        store.SetReminder(note.Id, later);

        Assert.Empty(store.GetDueReminders(DateTime.UtcNow));
        var due = store.GetDueReminders(later.AddSeconds(1));
        Assert.Single(due);
        Assert.False(due[0].ReminderFired);
    }

    [Fact]
    public void Reminder_can_be_cleared()
    {
        using var dir = new TempDir();
        var store = new NoteStore(Path.Combine(dir.Path, "notes.db"));
        var note = new Note { ReminderUtc = DateTime.UtcNow.AddHours(1) };
        store.Save(note);

        store.SetReminder(note.Id, null);

        Assert.Null(store.GetById(note.Id)!.ReminderUtc);
    }

    [Fact]
    public void Checklist_items_survive_save_and_load()
    {
        using var dir = new TempDir();
        var store = new NoteStore(Path.Combine(dir.Path, "notes.db"));
        var note = new Note { IsChecklist = true };
        note.Items.Add(new ChecklistItem { Text = "شیر", Done = false });
        note.Items.Add(new ChecklistItem { Text = "نان", Done = true });
        store.Save(note);

        var loaded = store.GetById(note.Id)!;
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal("نان", loaded.Items[1].Text);
        Assert.True(loaded.Items[1].Done);
    }
}
