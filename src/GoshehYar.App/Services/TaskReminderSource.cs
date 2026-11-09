using System;
using System.Collections.Generic;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Reminders;
using GoshehYar.Core.Tasks;

namespace GoshehYar.App.Services;

/// <summary>Exact reminders of to-do tasks.</summary>
public sealed class TaskReminderSource : IReminderSource
{
    private readonly AppController _controller;

    public TaskReminderSource(AppController controller) => _controller = controller;

    public IReadOnlyList<ReminderItem> TakeDue(DateTime nowUtc)
    {
        var items = new List<ReminderItem>();
        var store = _controller.TaskStore;
        if (store is null) return items;

        var lang = _controller.Settings.Current.Language;
        foreach (var task in store.GetDueReminders(nowUtc))
        {
            try
            {
                store.MarkReminderFired(task.Id);

                var title = string.IsNullOrWhiteSpace(task.Title) ? Loc.Get("task_untitled", lang) : task.Title.Trim();
                var body = "";
                if (task.Due is { } due)
                {
                    var when = DateNames.NumericDate(due, _controller.Settings.Current.CalendarKind);
                    if (task.DueHasTime) when += " " + due.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                    body = string.Format(Loc.Get("task_reminder_due", lang), when);
                }

                if (task.Note.Trim().Length > 0) body += (body.Length > 0 ? Environment.NewLine : "") + task.Note.Trim();
                items.Add(new ReminderItem(task.Id, title, DigitFormatter.Format(body, _controller.Settings.Current.Digits),
                    task.ReminderUtc, CanSnooze: true, Header: Loc.Get("task_reminder_header", lang)));
            }
            catch (Exception ex)
            {
                Logger.Error("Announcing a task reminder failed.", ex);
            }
        }

        _controller.RaiseTasksChanged();
        return items;
    }

    public void Snooze(string itemId, int minutes) => _controller.SnoozeTask(itemId, minutes);

    public void Open(string itemId) => _controller.OpenTask(itemId);
}
