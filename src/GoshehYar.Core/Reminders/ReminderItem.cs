namespace GoshehYar.Core.Reminders;

/// <summary>
/// One thing to announce in the reminder popup (a note, an event, a task, a finished timer).
/// <paramref name="Header"/> replaces the default window heading when set.
/// </summary>
public sealed record ReminderItem(
    string Id,
    string Title,
    string Body,
    DateTime? DueUtc,
    bool CanSnooze,
    string? Header = null);
