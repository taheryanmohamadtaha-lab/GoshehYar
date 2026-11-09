using System;
using System.Collections.Generic;
using System.Globalization;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Reminders;

namespace GoshehYar.App.Services;

/// <summary>Exact reminders of calendar events.</summary>
public sealed class CalendarReminderSource : IReminderSource
{
    private readonly AppController _controller;

    public CalendarReminderSource(AppController controller) => _controller = controller;

    public IReadOnlyList<ReminderItem> TakeDue(DateTime nowUtc)
    {
        var items = new List<ReminderItem>();
        var store = _controller.Events;
        if (store is null) return items;

        var s = _controller.Settings.Current;
        foreach (var ev in store.GetDueReminders(nowUtc))
        {
            try
            {
                store.MarkReminderFired(ev.Id, nowUtc);

                var title = string.IsNullOrWhiteSpace(ev.Title) ? Loc.Get("cal_untitled", s.Language) : ev.Title.Trim();
                var date = DateNames.LongDate(ev.Date, s.CalendarKind, s.Language);
                var body = ev.AllDay
                    ? date + " — " + Loc.Get("cal_all_day", s.Language)
                    : date + " " + TimeSpan.FromMinutes(ev.TimeMinutes).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
                if (ev.Note.Trim().Length > 0) body += Environment.NewLine + ev.Note.Trim();

                items.Add(new ReminderItem(ev.Id, title, DigitFormatter.Format(body, s.Digits), ev.ReminderUtc, CanSnooze: true,
                    Header: Loc.Get("cal_reminder_header", s.Language)));
            }
            catch (Exception ex)
            {
                Logger.Error("Announcing an event reminder failed.", ex);
            }
        }

        return items;
    }

    public void Snooze(string itemId, int minutes)
    {
        try
        {
            _controller.Events?.Snooze(itemId, minutes, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logger.Error("Snoozing an event reminder failed.", ex);
        }
    }

    public void Open(string itemId) => _controller.OpenEvent(itemId);
}
