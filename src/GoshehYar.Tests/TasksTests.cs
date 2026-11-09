using GoshehYar.Core.Tasks;

namespace GoshehYar.Tests;

public class TaskFilterTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 14, 30, 0);

    private static TaskItem T(string title, DateTime? due = null, bool hasTime = false, bool done = false, TaskPriority p = TaskPriority.Normal) =>
        new() { Title = title, Due = due, DueHasTime = hasTime, Done = done, Priority = p, CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public void Today_contains_due_today_and_overdue_but_not_done_or_future()
    {
        var tasks = new[]
        {
            T("overdue", new DateTime(2026, 11, 1)),
            T("today", new DateTime(2026, 11, 9)),
            T("tomorrow", new DateTime(2026, 11, 10)),
            T("nodate"),
            T("donetoday", new DateTime(2026, 11, 9), done: true)
        };
        var titles = TaskFilters.Apply(tasks, TaskFilter.Today, "", "", Now).Select(t => t.Title).ToList();
        Assert.Equal(new[] { "overdue", "today" }, titles);
    }

    [Fact]
    public void Midnight_edges_belong_to_the_correct_day()
    {
        var justBefore = T("a", new DateTime(2026, 11, 9, 23, 59, 0), true);
        var midnight = T("b", new DateTime(2026, 11, 10, 0, 0, 0), true);

        var late = new DateTime(2026, 11, 9, 23, 59, 59);
        Assert.True(TaskFilters.Matches(justBefore, TaskFilter.Today, late));
        Assert.False(TaskFilters.Matches(midnight, TaskFilter.Today, late));
        Assert.True(TaskFilters.Matches(midnight, TaskFilter.Upcoming, late));

        var after = new DateTime(2026, 11, 10, 0, 0, 0);
        Assert.True(TaskFilters.Matches(midnight, TaskFilter.Today, after));
        Assert.False(TaskFilters.Matches(midnight, TaskFilter.Upcoming, after));
        Assert.True(TaskFilters.IsOverdue(justBefore, after));
    }

    [Fact]
    public void Overdue_rules_for_timed_and_date_only_tasks()
    {
        var timed = T("t", new DateTime(2026, 11, 9, 14, 0, 0), true);
        var dateOnly = T("d", new DateTime(2026, 11, 9));
        Assert.True(TaskFilters.IsOverdue(timed, Now));
        Assert.False(TaskFilters.IsOverdue(dateOnly, Now));
        Assert.True(TaskFilters.IsOverdue(dateOnly, Now.Date.AddDays(1)));
        Assert.False(TaskFilters.IsOverdue(T("x", new DateTime(2026, 11, 1), done: true), Now));
        Assert.False(TaskFilters.IsOverdue(T("n"), Now));
    }

    [Fact]
    public void Upcoming_nodate_done_and_all()
    {
        var tasks = new[]
        {
            T("up", new DateTime(2026, 12, 1)),
            T("none"),
            T("done", done: true),
            T("today", new DateTime(2026, 11, 9))
        };
        Assert.Equal(new[] { "up" }, TaskFilters.Apply(tasks, TaskFilter.Upcoming, null, null, Now).Select(t => t.Title));
        Assert.Equal(new[] { "none" }, TaskFilters.Apply(tasks, TaskFilter.NoDate, null, null, Now).Select(t => t.Title));
        Assert.Equal(new[] { "done" }, TaskFilters.Apply(tasks, TaskFilter.Done, null, null, Now).Select(t => t.Title));
        Assert.Equal(4, TaskFilters.Apply(tasks, TaskFilter.All, null, null, Now).Count);
    }

    [Fact]
    public void Counts_match_the_filters()
    {
        var tasks = new[]
        {
            T("overdue", new DateTime(2026, 11, 1)),
            T("today", new DateTime(2026, 11, 9)),
            T("up", new DateTime(2026, 12, 1)),
            T("none"),
            T("done", done: true)
        };
        var c = TaskFilters.Count(tasks, Now);
        Assert.Equal(new TaskCounts(2, 1, 1, 5, 1, 1), c);
    }

    [Fact]
    public void Category_and_search_filter()
    {
        var a = T("Buy milk"); a.Category = "Home";
        var b = T("Write report"); b.Category = "Work"; b.Note = "quarterly MILK numbers";
        var tasks = new[] { a, b };
        Assert.Single(TaskFilters.Apply(tasks, TaskFilter.All, "home", "", Now));
        Assert.Equal(2, TaskFilters.Apply(tasks, TaskFilter.All, "", "milk", Now).Count);
        Assert.Single(TaskFilters.Apply(tasks, TaskFilter.All, "Work", "milk", Now));
        Assert.Equal(new[] { "Home", "Work" }, TaskFilters.Categories(tasks));
    }

    [Fact]
    public void Sorting_puts_done_last_then_due_then_priority()
    {
        var tasks = new[]
        {
            T("done", new DateTime(2026, 11, 1), done: true),
            T("nodate-urgent", null, p: TaskPriority.Urgent),
            T("later", new DateTime(2026, 11, 20), p: TaskPriority.Urgent),
            T("soon-low", new DateTime(2026, 11, 10), p: TaskPriority.Low),
            T("soon-high", new DateTime(2026, 11, 10), p: TaskPriority.High),
            T("soon-timed", new DateTime(2026, 11, 10, 8, 0, 0), true, p: TaskPriority.Low)
        };
        var sorted = TaskFilters.Apply(tasks, TaskFilter.All, null, null, Now).Select(t => t.Title).ToList();
        Assert.Equal(new[] { "soon-timed", "soon-high", "soon-low", "later", "nodate-urgent", "done" }, sorted);
    }

    [Fact]
    public void Reminder_moment_uses_offset_and_default_hour()
    {
        var t = T("r", new DateTime(2026, 11, 9, 10, 0, 0), true);
        t.ReminderOffsetMinutes = 30;
        Assert.Equal(new DateTime(2026, 11, 9, 9, 30, 0), TaskFilters.ComputeReminderUtc(t, TimeZoneInfo.Utc));

        var d = T("d", new DateTime(2026, 11, 9));
        d.ReminderOffsetMinutes = 0;
        Assert.Equal(new DateTime(2026, 11, 9, 9, 0, 0), TaskFilters.ComputeReminderUtc(d, TimeZoneInfo.Utc));

        d.ReminderOffsetMinutes = null;
        Assert.Null(TaskFilters.ComputeReminderUtc(d, TimeZoneInfo.Utc));
        d.ReminderOffsetMinutes = 5;
        d.Due = null;
        Assert.Null(TaskFilters.ComputeReminderUtc(d, TimeZoneInfo.Utc));
    }
}

public class TaskStoreTests
{
    [Fact]
    public void Task_round_trips_through_the_database()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "notes.db");
        var created = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var task = new TaskItem
        {
            Title = "Pay rent",
            Note = "line1\nline2",
            Priority = TaskPriority.Urgent,
            Category = "Home",
            Due = new DateTime(2026, 11, 12, 17, 45, 0),
            DueHasTime = true,
            Done = true,
            CompletedUtc = created.AddDays(1),
            CreatedUtc = created,
            ModifiedUtc = created.AddHours(2),
            ReminderOffsetMinutes = 60,
            ReminderUtc = created.AddDays(10),
            ReminderFired = false
        };
        new TaskStore(path).Save(task);

        var loaded = new TaskStore(path).GetAll().Single();
        Assert.Equal(task.Id, loaded.Id);
        Assert.Equal("Pay rent", loaded.Title);
        Assert.Equal("line1\nline2", loaded.Note);
        Assert.Equal(TaskPriority.Urgent, loaded.Priority);
        Assert.Equal("Home", loaded.Category);
        Assert.Equal(new DateTime(2026, 11, 12, 17, 45, 0), loaded.Due);
        Assert.True(loaded.DueHasTime);
        Assert.True(loaded.Done);
        Assert.Equal(task.CompletedUtc, loaded.CompletedUtc);
        Assert.Equal(created, loaded.CreatedUtc);
        Assert.Equal(60, loaded.ReminderOffsetMinutes);
        Assert.Equal(task.ReminderUtc, loaded.ReminderUtc);
    }

    [Fact]
    public void Null_due_and_delete_and_clear_completed()
    {
        using var dir = new TempDir();
        var store = new TaskStore(Path.Combine(dir.Path, "notes.db"));
        var a = new TaskItem { Title = "a" };
        var b = new TaskItem { Title = "b", Done = true };
        var c = new TaskItem { Title = "c", Done = true };
        store.Save(a); store.Save(b); store.Save(c);

        Assert.Null(store.GetById(a.Id)!.Due);
        Assert.Equal(2, store.DeleteCompleted());
        Assert.Single(store.GetAll());
        store.Delete(a.Id);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Due_reminders_are_returned_once_and_ignore_done_tasks()
    {
        using var dir = new TempDir();
        var store = new TaskStore(Path.Combine(dir.Path, "notes.db"));
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        var due = new TaskItem { Title = "due", Due = new DateTime(2026, 11, 9, 12, 0, 0), DueHasTime = true, ReminderOffsetMinutes = 0, ReminderUtc = now.AddMinutes(-1) };
        var future = new TaskItem { Title = "future", ReminderOffsetMinutes = 0, ReminderUtc = now.AddHours(1) };
        var finished = new TaskItem { Title = "finished", Done = true, ReminderOffsetMinutes = 0, ReminderUtc = now.AddMinutes(-5) };
        store.Save(due); store.Save(future); store.Save(finished);

        var first = store.GetDueReminders(now);
        Assert.Equal(new[] { due.Id }, first.Select(t => t.Id));

        store.MarkReminderFired(due.Id);
        Assert.Empty(store.GetDueReminders(now));
        Assert.Single(store.GetDueReminders(now.AddHours(2)));

        // Snooze re-arms it.
        store.SetReminder(due.Id, now.AddMinutes(10));
        Assert.Empty(store.GetDueReminders(now));
        Assert.Contains(store.GetDueReminders(now.AddMinutes(11)), t => t.Id == due.Id);
    }

    [Fact]
    public void Save_does_not_overwrite_reminder_columns_of_existing_rows()
    {
        using var dir = new TempDir();
        var store = new TaskStore(Path.Combine(dir.Path, "notes.db"));
        var t = new TaskItem { Title = "x", ReminderOffsetMinutes = 0, ReminderUtc = new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc) };
        store.Save(t);
        store.MarkReminderFired(t.Id);

        t.Title = "renamed"; // stale copy with ReminderFired=false
        store.Save(t);

        var loaded = store.GetById(t.Id)!;
        Assert.Equal("renamed", loaded.Title);
        Assert.True(loaded.ReminderFired);

        store.SetReminderSchedule(t.Id, 15, new DateTime(2026, 11, 9, 11, 0, 0, DateTimeKind.Utc));
        loaded = store.GetById(t.Id)!;
        Assert.Equal(15, loaded.ReminderOffsetMinutes);
        Assert.False(loaded.ReminderFired);
    }
}
