using System;
using System.Collections.Generic;
using GoshehYar.Core.Reminders;

namespace GoshehYar.App.Services;

/// <summary>
/// Something that has reminders (notes, calendar events, tasks). The app asks every source every few seconds
/// which reminders are due. <see cref="TakeDue"/> must mark them as announced, so each one is returned once.
/// </summary>
public interface IReminderSource
{
    /// <summary>Returns reminders that are due at <paramref name="nowUtc"/> and marks them as announced.</summary>
    IReadOnlyList<ReminderItem> TakeDue(DateTime nowUtc);

    /// <summary>Postpones an announced item by the given minutes.</summary>
    void Snooze(string itemId, int minutes);

    /// <summary>Opens the tool and shows the item (the "open" button of the popup).</summary>
    void Open(string itemId);
}
