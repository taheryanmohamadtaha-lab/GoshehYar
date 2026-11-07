using System;
using System.Collections.Generic;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Reminders;

namespace GoshehYar.App.Services;

/// <summary>Reminders of notes and checklists.</summary>
public sealed class NotesReminderSource : IReminderSource
{
    private readonly AppController _controller;

    public NotesReminderSource(AppController controller) => _controller = controller;

    public IReadOnlyList<ReminderItem> TakeDue(DateTime nowUtc)
    {
        var items = new List<ReminderItem>();
        var store = _controller.Notes;
        if (store is null) return items;

        foreach (var note in store.GetDueReminders(nowUtc))
        {
            try
            {
                // Only the "announced" flag is written, so unsaved edits in the notes window are never overwritten.
                store.MarkReminderFired(note.Id);
                _controller.RaiseNoteReminderFired(note.Id);

                var title = string.IsNullOrWhiteSpace(note.Title)
                    ? Loc.Get("note_untitled", _controller.Settings.Current.Language)
                    : note.Title.Trim();

                string body;
                if (note.IsChecklist)
                {
                    var open = new List<string>();
                    foreach (var i in note.Items)
                    {
                        if (!i.Done && open.Count < 6) open.Add("☐ " + i.Text);
                    }

                    body = string.Join(Environment.NewLine, open);
                }
                else
                {
                    body = note.Body.Trim();
                    if (body.Length > 240) body = body[..240] + "…";
                }

                items.Add(new ReminderItem(note.Id, title, body, note.ReminderUtc, CanSnooze: true));
            }
            catch (Exception ex)
            {
                Logger.Error("Announcing a note reminder failed.", ex);
            }
        }

        return items;
    }

    public void Snooze(string itemId, int minutes) => _controller.SnoozeNote(itemId, minutes);

    public void Open(string itemId) => _controller.OpenNote(itemId);
}
