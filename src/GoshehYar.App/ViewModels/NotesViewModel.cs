using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Notes;
using GoshehYar.Core.Settings;
using Microsoft.Win32;

namespace GoshehYar.App.ViewModels;

public sealed record ColorOption(string Key, Brush Brush, string Label);

public sealed class ChecklistItemVM : ViewModelBase
{
    private readonly ChecklistItem _model;
    private readonly Action _changed;

    public ChecklistItemVM(ChecklistItem model, Action changed)
    {
        _model = model;
        _changed = changed;
    }

    public ChecklistItem Model => _model;

    public string Text
    {
        get => _model.Text;
        set { _model.Text = value ?? ""; OnPropertyChanged(); _changed(); }
    }

    public bool Done
    {
        get => _model.Done;
        set { _model.Done = value; OnPropertyChanged(); _changed(); }
    }
}

/// <summary>One note in the editor. Every change is reported to the owner, which saves it automatically.</summary>
public sealed class NoteItemVM : ViewModelBase
{
    private readonly Action<NoteItemVM> _changed;

    public NoteItemVM(Note model, Action<NoteItemVM> changed)
    {
        Model = model;
        _changed = changed;
        Items = new ObservableCollection<ChecklistItemVM>(model.Items.Select(i => new ChecklistItemVM(i, Touch)));
    }

    public Note Model { get; }

    public ObservableCollection<ChecklistItemVM> Items { get; }

    public bool IsChecklist => Model.IsChecklist;

    public string Title
    {
        get => Model.Title;
        set { Model.Title = value ?? ""; Touch(); OnPropertyChanged(); OnPropertyChanged(nameof(ListTitle)); }
    }

    public string Body
    {
        get => Model.Body;
        set { Model.Body = value ?? ""; Touch(); OnPropertyChanged(); OnPropertyChanged(nameof(Preview)); OnPropertyChanged(nameof(ListTitle)); }
    }

    public string Category
    {
        get => Model.Category;
        set { Model.Category = (value ?? "").Trim(); Touch(); OnPropertyChanged(); }
    }

    public string Color
    {
        get => Model.Color;
        set { Model.Color = value ?? ""; Touch(); OnPropertyChanged(); OnPropertyChanged(nameof(ColorBrush)); }
    }

    public bool Pinned
    {
        get => Model.Pinned;
        set { Model.Pinned = value; Touch(); OnPropertyChanged(); }
    }

    public Brush ColorBrush => NotesViewModel.BrushFor(Model.Color);

    public string ListTitle
    {
        get
        {
            if (Model.Title.Trim().Length > 0) return Model.Title.Trim();
            var line = Model.Body.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return line ?? UntitledProvider();
        }
    }

    /// <summary>Text shown in the list for a note without title or text (set by the window's view model for the current language).</summary>
    public static Func<string> UntitledProvider { get; set; } = () => "";

    public string Preview
    {
        get
        {
            if (Model.IsChecklist)
            {
                var done = Model.Items.Count(i => i.Done);
                return $"{done}/{Model.Items.Count}";
            }

            var lines = Model.Body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (Model.Title.Trim().Length == 0 && lines.Count > 0) lines.RemoveAt(0);
            var text = lines.FirstOrDefault() ?? "";
            return text.Length > 70 ? text[..70] + "…" : text;
        }
    }

    // ---- reminder (local time, down to the minute) ----

    private DateTime? ReminderLocal => Model.ReminderUtc?.ToLocalTime();

    public bool HasReminder
    {
        get => Model.ReminderUtc is not null;
        set
        {
            if (value == HasReminder) return;
            if (value)
            {
                // Default: the next full hour.
                var next = DateTime.Now.AddHours(1);
                SetReminderLocal(new DateTime(next.Year, next.Month, next.Day, next.Hour, 0, 0));
            }
            else
            {
                Model.ReminderUtc = null;
                Model.ReminderFired = false;
                Touch();
                RaiseReminderChanged();
            }
        }
    }

    public DateTime? ReminderDate
    {
        get => ReminderLocal?.Date;
        set
        {
            if (value is null)
            {
                // The date box was emptied: that means "no reminder".
                if (HasReminder) HasReminder = false;
                return;
            }

            var time = ReminderLocal?.TimeOfDay ?? new TimeSpan(9, 0, 0);
            SetReminderLocal(value.Value.Date + new TimeSpan(time.Hours, time.Minutes, 0));
        }
    }

    public int ReminderHour
    {
        get => ReminderLocal?.Hour ?? 9;
        set
        {
            if (ReminderLocal is not { } t || value < 0 || value > 23) return;
            SetReminderLocal(t.Date + new TimeSpan(value, t.Minute, 0));
        }
    }

    public int ReminderMinute
    {
        get => ReminderLocal?.Minute ?? 0;
        set
        {
            if (ReminderLocal is not { } t || value < 0 || value > 59) return;
            SetReminderLocal(t.Date + new TimeSpan(t.Hour, value, 0));
        }
    }

    /// <summary>Reminder moment as local time, for display (null when there is none).</summary>
    public DateTime? ReminderMoment => ReminderLocal;

    public bool ReminderFired => Model.ReminderFired;

    private void SetReminderLocal(DateTime local)
    {
        Model.ReminderUtc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
        Model.ReminderFired = false;
        Touch();
        RaiseReminderChanged();
    }

    public void ClearReminder() => HasReminder = false;

    public void MarkReminderFired()
    {
        Model.ReminderFired = true;
        RaiseReminderChanged();
    }

    /// <summary>The reminder was changed elsewhere (snooze). The database is already up to date.</summary>
    public void ApplyExternalReminder(DateTime? utc, bool fired)
    {
        Model.ReminderUtc = utc;
        Model.ReminderFired = fired;
        RaiseReminderChanged();
    }

    private void RaiseReminderChanged()
    {
        OnPropertyChanged(nameof(HasReminder));
        OnPropertyChanged(nameof(ReminderDate));
        OnPropertyChanged(nameof(ReminderHour));
        OnPropertyChanged(nameof(ReminderMinute));
        OnPropertyChanged(nameof(ReminderMoment));
        OnPropertyChanged(nameof(ReminderFired));
    }

    // ---- checklist ----

    public void AddItem(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var item = new ChecklistItem { Text = text.Trim() };
        Model.Items.Add(item);
        Items.Add(new ChecklistItemVM(item, Touch));
        Touch();
        OnPropertyChanged(nameof(Preview));
    }

    public void RemoveItem(ChecklistItemVM item)
    {
        Model.Items.Remove(item.Model);
        Items.Remove(item);
        Touch();
        OnPropertyChanged(nameof(Preview));
    }

    private void Touch()
    {
        Model.ModifiedUtc = DateTime.UtcNow;
        OnPropertyChanged(nameof(Preview));
        _changed(this);
    }
}

public sealed class NotesViewModel : LocalizedViewModel
{
    private readonly NoteStore _store;
    private readonly List<NoteItemVM> _all = new();
    private readonly HashSet<NoteItemVM> _dirty = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _summaryTimer = new() { Interval = TimeSpan.FromSeconds(20) };

    private NoteItemVM? _selected;
    private string _search = "";
    private string _filterCategory = "";
    private string _newItemText = "";
    private string _status = "";
    private bool _rebuilding;
    private bool _refilterPending;

    public NotesViewModel(AppController controller) : base(controller)
    {
        _store = controller.Notes ?? throw new InvalidOperationException("Notes database is not available.");

        NoteItemVM.UntitledProvider = () => this["note_untitled"];
        NewNoteCommand = new RelayCommand(() => NewNote(false));
        NewChecklistCommand = new RelayCommand(() => NewNote(true));
        DeleteCommand = new RelayCommand(DeleteSelected);
        AddItemCommand = new RelayCommand(AddItem);
        RemoveItemCommand = new RelayCommand(p => { if (p is ChecklistItemVM i) Selected?.RemoveItem(i); });
        ClearReminderCommand = new RelayCommand(() => Selected?.ClearReminder());
        ExportTextCommand = new RelayCommand(() => Export(markdown: false));
        ExportMarkdownCommand = new RelayCommand(() => Export(markdown: true));

        Colors = new[]
        {
            new ColorOption("", Brushes.Transparent, this["color_none"]),
            new ColorOption("yellow", BrushFor("yellow"), this["color_yellow"]),
            new ColorOption("green", BrushFor("green"), this["color_green"]),
            new ColorOption("blue", BrushFor("blue"), this["color_blue"]),
            new ColorOption("pink", BrushFor("pink"), this["color_pink"]),
            new ColorOption("purple", BrushFor("purple"), this["color_purple"]),
            new ColorOption("orange", BrushFor("orange"), this["color_orange"]),
        };

        _saveTimer.Tick += (_, _) => SaveDirty();
        _summaryTimer.Tick += (_, _) => OnPropertyChanged(nameof(ReminderSummary));
        _summaryTimer.Start();
        Controller.NoteReminderFired += OnReminderFired;
        Controller.NoteReminderChanged += OnReminderChanged;
        Controller.NotesFlushRequested += FlushNow;

        foreach (var note in _store.GetAll()) _all.Add(Wrap(note));
        Refilter(null);
    }

    public static Brush BrushFor(string key)
    {
        var hex = key switch
        {
            "yellow" => "#FFE680",
            "green" => "#A8E6A1",
            "blue" => "#9CCBFF",
            "pink" => "#FFB3D1",
            "purple" => "#D6B3FF",
            "orange" => "#FFC48A",
            _ => null
        };

        return hex is null ? Brushes.Transparent : ThemeManager.MakeBrush(hex);
    }

    public ICommand NewNoteCommand { get; }
    public ICommand NewChecklistCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand ClearReminderCommand { get; }
    public ICommand ExportTextCommand { get; }
    public ICommand ExportMarkdownCommand { get; }

    public IReadOnlyList<ColorOption> Colors { get; }

    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToList();

    public IReadOnlyList<int> Minutes { get; } = Enumerable.Range(0, 60).ToList();

    /// <summary>"Reminder: 2026-11-08 09:30 (in 2 hours 5 minutes)" for the selected note.</summary>
    public string ReminderSummary
    {
        get
        {
            if (_selected?.ReminderMoment is not { } moment) return this["notes_no_reminder"];

            var when = Num(moment.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            var diff = moment - DateTime.Now;
            string relative;
            if (_selected.ReminderFired) relative = this["rel_announced"];
            else if (diff.TotalSeconds >= 60) relative = Fmt("rel_in", Span(diff));
            else if (diff.TotalSeconds > -60) relative = this["rel_now"];
            else relative = Fmt("rel_ago", Span(-diff));

            return Fmt("notes_reminder_at", when, relative);
        }
    }

    private string Span(TimeSpan span)
    {
        var parts = new List<string>();
        if (span.Days > 0) parts.Add(Fmt("unit_day", span.Days));
        if (span.Hours > 0) parts.Add(Fmt("unit_hour", span.Hours));
        if (span.Days == 0 && span.Minutes > 0) parts.Add(Fmt("unit_minute", span.Minutes));
        if (parts.Count == 0) parts.Add(Fmt("unit_minute", 1));
        return string.Join(this["unit_and"], parts);
    }

    public ObservableCollection<NoteItemVM> Notes { get; } = new();

    /// <summary>Categories for the filter box; first entry (empty value) means "all".</summary>
    public ObservableCollection<OptionItem<string>> FilterCategories { get; } = new();

    /// <summary>Existing categories offered when editing a note.</summary>
    public ObservableCollection<string> CategoryChoices { get; } = new();

    public NoteItemVM? Selected
    {
        get => _selected;
        set
        {
            if (_rebuilding) return;

            // Whatever was typed so far is written to disk before another note is shown.
            if (!ReferenceEquals(_selected, value)) SaveDirty(allowRefilter: false);

            _selected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReminderSummary));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(ShowText));
            OnPropertyChanged(nameof(ShowChecklist));
            OnPropertyChanged(nameof(MetaText));
        }
    }

    public bool HasSelection => _selected is not null;
    public bool ShowText => _selected is { IsChecklist: false };
    public bool ShowChecklist => _selected is { IsChecklist: true };
    public bool ShowEmptyHint => _selected is null;

    public string SearchText
    {
        get => _search;
        set
        {
            var text = value ?? "";
            if (_rebuilding || text == _search) return;
            _search = text;
            OnPropertyChanged();
            Refilter(_selected);
        }
    }

    public string FilterCategory
    {
        get => _filterCategory;
        set
        {
            // The category box is emptied and refilled while the list is rebuilt; those writes must be ignored,
            // otherwise rebuilding triggers another rebuild without end.
            var category = value ?? "";
            if (_rebuilding || category == _filterCategory) return;
            _filterCategory = category;
            OnPropertyChanged();
            Refilter(_selected);
        }
    }

    public string NewItemText
    {
        get => _newItemText;
        set { _newItemText = value ?? ""; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public string CountText => Fmt("notes_count", Notes.Count, _all.Count);

    public string MetaText
    {
        get
        {
            if (_selected is null) return "";
            var c = _selected.Model.CreatedUtc.ToLocalTime();
            var m = _selected.Model.ModifiedUtc.ToLocalTime();
            return Fmt("notes_meta", Num(c.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)), Num(m.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
        }
    }

    // ----------------------------------------------------------------- editing

    private NoteItemVM Wrap(Note note)
    {
        var item = new NoteItemVM(note, OnNoteChanged);
        item.PropertyChanged += (_, e) =>
        {
            // Pin and category change where a note appears in the list.
            if (e.PropertyName is nameof(NoteItemVM.Pinned) or nameof(NoteItemVM.Category)) _refilterPending = true;
            if (e.PropertyName is nameof(NoteItemVM.ReminderMoment) && item == _selected) OnPropertyChanged(nameof(ReminderSummary));
        };
        return item;
    }

    private void OnNoteChanged(NoteItemVM item)
    {
        if (_rebuilding) return;
        _dirty.Add(item);
        _saveTimer.Stop();
        _saveTimer.Start();
        if (item == _selected) OnPropertyChanged(nameof(MetaText));
    }

    private void NewNote(bool checklist)
    {
        var note = new Note { IsChecklist = checklist };
        if (FilterCategory.Length > 0) note.Category = FilterCategory;

        var item = Wrap(note);
        _all.Add(item);
        _dirty.Add(item);
        SaveDirty();

        // A fresh note must be visible even if a search is active.
        _search = "";
        OnPropertyChanged(nameof(SearchText));
        Refilter(item);
    }

    private void AddItem()
    {
        if (Selected is null) return;
        Selected.AddItem(NewItemText);
        NewItemText = "";
    }

    private void DeleteSelected()
    {
        var item = Selected;
        if (item is null) return;

        var answer = MessageBox.Show(
            this["notes_confirm_delete"],
            this["notes_delete"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            S.Language == AppLanguage.Persian ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            _dirty.Remove(item);
            _store.Delete(item.Model.Id);
            _all.Remove(item);
            Refilter(null);
            Status = this["notes_deleted"];
        }
        catch (Exception ex)
        {
            Logger.Error("Deleting a note failed.", ex);
            Status = this["notes_save_error"];
        }
    }

    // ------------------------------------------------------------- save / sync

    private void SaveDirty(bool allowRefilter = true)
    {
        _saveTimer.Stop();
        if (_dirty.Count == 0) return;

        var failed = false;
        foreach (var item in _dirty.ToList())
        {
            try
            {
                _store.Save(item.Model);
                _dirty.Remove(item);
            }
            catch (Exception ex)
            {
                failed = true;
                Logger.Error("Saving a note failed.", ex);
            }
        }

        Status = failed ? this["notes_save_error"] : this["notes_saved"];

        // Rebuilding the list on every autosave would steal the cursor while typing.
        if (_refilterPending && allowRefilter)
        {
            _refilterPending = false;
            Refilter(_selected);
        }
    }

    /// <summary>Writes pending edits right now (before a backup or when the window closes).</summary>
    public void FlushNow() => SaveDirty();

    private void OnReminderFired(string id)
    {
        var item = _all.FirstOrDefault(n => n.Model.Id == id);
        item?.MarkReminderFired();
        if (item == _selected) OnPropertyChanged(nameof(ReminderSummary));
    }

    private void OnReminderChanged(string id, DateTime? utc)
    {
        var item = _all.FirstOrDefault(n => n.Model.Id == id);
        item?.ApplyExternalReminder(utc, fired: false);
        if (item == _selected) OnPropertyChanged(nameof(ReminderSummary));
    }

    /// <summary>Shows a given note (used by the reminder popup's "open note" button).</summary>
    public void SelectById(string id)
    {
        var item = _all.FirstOrDefault(n => n.Model.Id == id);
        if (item is null) return;

        _search = "";
        _filterCategory = "";
        OnPropertyChanged(nameof(SearchText));
        Refilter(item);
    }

    // -------------------------------------------------------------- list state

    private void Refilter(NoteItemVM? keepSelected)
    {
        _rebuilding = true;
        try
        {
            var filtered = NoteTools.Filter(_all.Select(a => a.Model), _search, _filterCategory);
            var byId = _all.ToDictionary(a => a.Model.Id);

            Notes.Clear();
            foreach (var note in filtered) Notes.Add(byId[note.Id]);

            RebuildCategories();
        }
        finally
        {
            _rebuilding = false;
        }

        _selected = keepSelected is not null && Notes.Contains(keepSelected) ? keepSelected : null;
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowText));
        OnPropertyChanged(nameof(ShowChecklist));
        OnPropertyChanged(nameof(ShowEmptyHint));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(CountText));
    }

    private void RebuildCategories()
    {
        var categories = NoteTools.Categories(_all.Select(a => a.Model));

        // Only touch the collections when something really changed: replacing the items of an editable
        // category box while the user types in it would wipe the text.
        if (!CategoryChoices.SequenceEqual(categories))
        {
            CategoryChoices.Clear();
            foreach (var c in categories) CategoryChoices.Add(c);
        }

        var wanted = new List<string> { "" };
        wanted.AddRange(categories);
        if (!FilterCategories.Select(o => o.Value).SequenceEqual(wanted))
        {
            FilterCategories.Clear();
            FilterCategories.Add(new OptionItem<string>("", this["notes_all_categories"]));
            foreach (var c in categories) FilterCategories.Add(new OptionItem<string>(c, c));
        }

        // A category that disappeared must not keep filtering everything out.
        if (_filterCategory.Length > 0 && !categories.Contains(_filterCategory, StringComparer.OrdinalIgnoreCase))
        {
            _filterCategory = "";
        }

        OnPropertyChanged(nameof(FilterCategory));
    }

    // ------------------------------------------------------------------ export

    private void Export(bool markdown)
    {
        var item = Selected;
        if (item is null) return;

        var dialog = new SaveFileDialog
        {
            FileName = NoteTools.SafeFileName(item.ListTitle, "note") + (markdown ? ".md" : ".txt"),
            Filter = markdown ? "Markdown|*.md" : "Text|*.txt",
            DefaultExt = markdown ? ".md" : ".txt",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dialog.FileName,
                markdown ? NoteTools.ToMarkdown(item.Model) : NoteTools.ToText(item.Model),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Status = Fmt("notes_exported", Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            Logger.Warn("Export failed: " + ex.Message);
            Status = this["notes_export_error"];
        }
    }

    public override void Dispose()
    {
        FlushNow();
        _saveTimer.Stop();
        _summaryTimer.Stop();
        Controller.NoteReminderFired -= OnReminderFired;
        Controller.NoteReminderChanged -= OnReminderChanged;
        Controller.NotesFlushRequested -= FlushNow;
        base.Dispose();
    }
}
