namespace GoshehYar.Core.Tasks;

public enum TaskFilter
{
    Today,
    Upcoming,
    NoDate,
    All,
    Done
}

public sealed record TaskCounts(int Today, int Upcoming, int NoDate, int All, int Done, int Overdue);

/// <summary>Pure filtering, sorting and date rules for tasks. "now" is always passed in (local time).</summary>
public static class TaskFilters
{
    /// <summary>Hour used as the reminder moment for tasks that have a due date but no time.</summary>
    public const int DefaultReminderHour = 9;

    public static bool IsOverdue(TaskItem t, DateTime nowLocal)
    {
        if (t.Done || t.Due is not { } due) return false;
        return t.DueHasTime ? due < nowLocal : due.Date < nowLocal.Date;
    }

    public static bool Matches(TaskItem t, TaskFilter filter, DateTime nowLocal)
    {
        var today = nowLocal.Date;
        return filter switch
        {
            TaskFilter.Today => !t.Done && t.Due is { } d && d.Date <= today,
            TaskFilter.Upcoming => !t.Done && t.Due is { } d && d.Date > today,
            TaskFilter.NoDate => !t.Done && t.Due is null,
            TaskFilter.Done => t.Done,
            _ => true
        };
    }

    public static TaskCounts Count(IEnumerable<TaskItem> tasks, DateTime nowLocal)
    {
        int today = 0, upcoming = 0, noDate = 0, all = 0, done = 0, overdue = 0;
        foreach (var t in tasks)
        {
            all++;
            if (t.Done) { done++; continue; }
            if (Matches(t, TaskFilter.Today, nowLocal)) today++;
            if (Matches(t, TaskFilter.Upcoming, nowLocal)) upcoming++;
            if (Matches(t, TaskFilter.NoDate, nowLocal)) noDate++;
            if (IsOverdue(t, nowLocal)) overdue++;
        }

        return new TaskCounts(today, upcoming, noDate, all, done, overdue);
    }

    /// <summary>Filters (state, category, search text) and sorts. An empty category means all categories.</summary>
    public static List<TaskItem> Apply(IEnumerable<TaskItem> tasks, TaskFilter filter, string? category, string? search, DateTime nowLocal)
    {
        var cat = (category ?? "").Trim();
        var q = (search ?? "").Trim();
        var result = tasks.Where(t =>
            Matches(t, filter, nowLocal)
            && (cat.Length == 0 || string.Equals(t.Category.Trim(), cat, StringComparison.OrdinalIgnoreCase))
            && (q.Length == 0
                || t.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || t.Note.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || t.Category.Contains(q, StringComparison.CurrentCultureIgnoreCase))).ToList();
        result.Sort(Compare);
        return result;
    }

    /// <summary>Open tasks first; then earlier due date (no date last); then higher priority; then older first. Done tasks: latest completed first.</summary>
    public static int Compare(TaskItem a, TaskItem b)
    {
        if (a.Done != b.Done) return a.Done ? 1 : -1;

        if (a.Done)
        {
            var c = Nullable.Compare(b.CompletedUtc, a.CompletedUtc);
            if (c != 0) return c;
        }

        var aNone = a.Due is null;
        var bNone = b.Due is null;
        if (aNone != bNone) return aNone ? 1 : -1;
        if (a.Due is { } da && b.Due is { } db)
        {
            var c = da.Date.CompareTo(db.Date);
            if (c != 0) return c;

            // Within one day tasks with a time come before those without; then by time.
            if (a.DueHasTime != b.DueHasTime) return a.DueHasTime ? -1 : 1;
            c = da.CompareTo(db);
            if (c != 0) return c;
        }

        var p = b.Priority.CompareTo(a.Priority);
        if (p != 0) return p;
        var created = a.CreatedUtc.CompareTo(b.CreatedUtc);
        if (created != 0) return created;
        return string.CompareOrdinal(a.Id, b.Id);
    }

    public static List<string> Categories(IEnumerable<TaskItem> tasks) =>
        tasks.Select(t => t.Category.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>The local moment a task is due for reminder purposes (date-only tasks use 09:00).</summary>
    public static DateTime? DueMoment(TaskItem t)
    {
        if (t.Due is not { } due) return null;
        return t.DueHasTime ? due : due.Date.AddHours(DefaultReminderHour);
    }

    /// <summary>UTC time the reminder should fire, or null when there is no reminder or no due date.</summary>
    public static DateTime? ComputeReminderUtc(TaskItem t, TimeZoneInfo? zone = null)
    {
        if (t.ReminderOffsetMinutes is not { } offset || DueMoment(t) is not { } moment) return null;
        var local = DateTime.SpecifyKind(moment.AddMinutes(-Math.Max(0, offset)), DateTimeKind.Unspecified);
        var tz = zone ?? TimeZoneInfo.Local;
        try
        {
            if (tz.IsInvalidTime(local)) local = local.AddHours(1);
            return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }
        catch (ArgumentException)
        {
            return DateTime.SpecifyKind(local, DateTimeKind.Utc);
        }
    }
}
