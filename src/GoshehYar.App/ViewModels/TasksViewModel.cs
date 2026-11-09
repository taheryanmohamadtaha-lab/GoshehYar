using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.Services;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Tasks;

namespace GoshehYar.App.ViewModels;

public sealed class TaskRow : ViewModelBase
{
    public TaskRow(TaskItem item, string meta, bool overdue)
    {
        Item = item;
        Meta = meta;
        Overdue = overdue;
    }

    public TaskItem Item { get; }
    public string Id => Item.Id;
    public string Title { get; init; } = "";
    public string Meta { get; }
    public bool Overdue { get; }
    public bool Done => Item.Done;
    public bool HasMeta => Meta.Length > 0;
    public bool HasReminder => Item.ReminderOffsetMinutes is not null && !Item.Done;
}

/// <summary>To-do list: quick add, filters, editor with due date/time, priority, category and exact reminder.</summary>
public sealed class TasksViewModel : LocalizedViewModel
{
    private readonly TaskStore? _store;
    private List<TaskItem> _all = new();
    private TaskFilter _filter = TaskFilter.Today;
    private string _category = "";
    private string _search = "";
    private string _newTitle = "";
    private string _status = "";
    private bool _rebuilding;
    private bool _loading;
    private TaskRow? _selected;
    private IReadOnlyList<OptionItem<TaskFilter>>? _filters;
    private IReadOnlyList<OptionItem<string>>? _categories;
    private IReadOnlyList<OptionItem<TaskPriority>>? _priorities;
    private IReadOnlyList<OptionItem<int>>? _reminderChoices;

    // editor state
    private string _editTitle = "", _editNote = "", _editCategory = "";
    private TaskPriority _editPriority = TaskPriority.Normal;
    private DateTime? _editDate;
    private bool _editHasTime;
    private int _editHour = 9, _editMinute;
    private int _editReminder = -1;

    public TasksViewModel(AppController controller) : base(controller)
    {
        _store = controller.TaskStore;
        AddCommand = new RelayCommand(Add);
        DeleteCommand = new RelayCommand(DeleteSelected);
        ClearDoneCommand = new RelayCommand(ClearDone);
        ToggleCommand = new RelayCommand(p => { if (p is TaskRow r) Toggle(r); });
        ClearDateCommand = new RelayCommand(() => EditDate = null);
        Reload();
    }

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearDoneCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand ClearDateCommand { get; }

    public bool DatabaseOk => _store is not null;
    public bool DatabaseFailed => _store is null;

    public ObservableCollection<TaskRow> Rows { get; } = new();
    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToList();
    public IReadOnlyList<int> Minutes { get; } = Enumerable.Range(0, 60).ToList();

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public string NewTitle
    {
        get => _newTitle;
        set { _newTitle = value ?? ""; OnPropertyChanged(); }
    }

    // ------------------------------------------------------------------ choices

    public IReadOnlyList<OptionItem<TaskFilter>> Filters
    {
        get
        {
            if (_filters is not null) return _filters;
            var c = TaskFilters.Count(_all, DateTime.Now);
            _filters = new[]
            {
                new OptionItem<TaskFilter>(TaskFilter.Today, Fmt("task_f_today", c.Today)),
                new OptionItem<TaskFilter>(TaskFilter.Upcoming, Fmt("task_f_upcoming", c.Upcoming)),
                new OptionItem<TaskFilter>(TaskFilter.NoDate, Fmt("task_f_nodate", c.NoDate)),
                new OptionItem<TaskFilter>(TaskFilter.All, Fmt("task_f_all", c.All)),
                new OptionItem<TaskFilter>(TaskFilter.Done, Fmt("task_f_done", c.Done)),
            };
            return _filters;
        }
    }

    public IReadOnlyList<OptionItem<string>> Categories
    {
        get
        {
            if (_categories is not null) return _categories;
            var list = new List<OptionItem<string>> { new("", this["task_all_categories"]) };
            list.AddRange(TaskFilters.Categories(_all).Select(c => new OptionItem<string>(c, c)));
            _categories = list;
            return list;
        }
    }

    public IReadOnlyList<OptionItem<TaskPriority>> Priorities => _priorities ??= new[]
    {
        new OptionItem<TaskPriority>(TaskPriority.Low, this["task_p_low"]),
        new OptionItem<TaskPriority>(TaskPriority.Normal, this["task_p_normal"]),
        new OptionItem<TaskPriority>(TaskPriority.High, this["task_p_high"]),
        new OptionItem<TaskPriority>(TaskPriority.Urgent, this["task_p_urgent"]),
    };

    /// <summary>Value -1 = no reminder, otherwise minutes before the due moment.</summary>
    public IReadOnlyList<OptionItem<int>> ReminderChoices => _reminderChoices ??= new[]
    {
        new OptionItem<int>(-1, this["task_reminder"] + ": —"),
        new OptionItem<int>(0, this["task_rem_at"]),
        new OptionItem<int>(5, Fmt("task_rem_min", 5)),
        new OptionItem<int>(15, Fmt("task_rem_min", 15)),
        new OptionItem<int>(30, Fmt("task_rem_min", 30)),
        new OptionItem<int>(60, Fmt("task_rem_hour", 1)),
        new OptionItem<int>(180, Fmt("task_rem_hour", 3)),
        new OptionItem<int>(1440, Fmt("task_rem_day", 1)),
        new OptionItem<int>(2880, Fmt("task_rem_day", 2)),
    };

    // ------------------------------------------------------------------ list state

    public TaskFilter Filter
    {
        get => _filter;
        set
        {
            if (_loading || value == _filter) return;
            _filter = value;
            OnPropertyChanged();
            Rebuild();
        }
    }

    public string CategoryFilter
    {
        get => _category;
        set
        {
            if (_loading || _rebuilding || value is null || value == _category) return;
            _category = value;
            OnPropertyChanged();
            Rebuild();
        }
    }

    public string SearchText
    {
        get => _search;
        set
        {
            _search = value ?? "";
            OnPropertyChanged();
            Rebuild();
        }
    }

    public string CountText
    {
        get
        {
            var counts = TaskFilters.Count(_all, DateTime.Now);
            var text = Fmt("task_count", Rows.Count, _all.Count);
            return counts.Overdue > 0 ? text + " · " + Fmt("task_overdue_count", counts.Overdue) : text;
        }
    }

    public bool IsEmpty => Rows.Count == 0;

    public TaskRow? Selected
    {
        get => _selected;
        set
        {
            if (_loading || _rebuilding) return;
            if (ReferenceEquals(value, _selected)) return;
            SaveEdits();
            _selected = value;
            LoadEditor();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(NoSelection));
        }
    }

    public bool HasSelection => _selected is not null;
    public bool NoSelection => _selected is null;

    // ------------------------------------------------------------------ editor properties

    public string EditTitle { get => _editTitle; set { _editTitle = value ?? ""; OnPropertyChanged(); } }
    public string EditNote { get => _editNote; set { _editNote = value ?? ""; OnPropertyChanged(); } }
    public string EditCategory { get => _editCategory; set { _editCategory = value ?? ""; OnPropertyChanged(); } }

    public TaskPriority EditPriority
    {
        get => _editPriority;
        set { _editPriority = value; OnPropertyChanged(); }
    }

    public DateTime? EditDate
    {
        get => _editDate;
        set
        {
            _editDate = value?.Date;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ReminderPossible));
            OnPropertyChanged(nameof(ReminderHint));
        }
    }

    public bool EditHasTime
    {
        get => _editHasTime;
        set { _editHasTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(ReminderHint)); }
    }

    public int EditHour { get => _editHour; set { _editHour = Math.Clamp(value, 0, 23); OnPropertyChanged(); } }
    public int EditMinute { get => _editMinute; set { _editMinute = Math.Clamp(value, 0, 59); OnPropertyChanged(); } }

    public int EditReminder
    {
        get => _editReminder;
        set { _editReminder = value; OnPropertyChanged(); }
    }

    public bool ReminderPossible => _editDate is not null;

    public string ReminderHint =>
        _editDate is null ? this["task_rem_needs_due"] : (!_editHasTime ? this["task_rem_default_time"] : "");

    public string DoneInfo
    {
        get
        {
            if (_selected?.Item is { Done: true, CompletedUtc: { } done })
                return Fmt("task_completed_at", Num(done.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)));
            return "";
        }
    }

    public string MetaText => _selected is null
        ? ""
        : Fmt("task_meta", Num(_selected.Item.CreatedUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)));

    // ------------------------------------------------------------------ operations

    private void Reload()
    {
        if (_store is null) return;
        try
        {
            _all = _store.GetAll().ToList();
        }
        catch (Exception ex)
        {
            Logger.Error("Loading tasks failed.", ex);
            Status = this["task_db_error"];
        }

        _filters = null;
        _categories = null;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_rebuilding) return;
        _rebuilding = true;
        try
        {
            var keepId = _selected?.Id;
            var now = DateTime.Now;
            var items = TaskFilters.Apply(_all, _filter, _category, _search, now);
            Rows.Clear();
            foreach (var t in items) Rows.Add(MakeRow(t, now));

            _selected = keepId is null ? null : Rows.FirstOrDefault(r => r.Id == keepId);
            _filters = null;
            _categories = null;

            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(NoSelection));
            OnPropertyChanged(nameof(Filters));
            OnPropertyChanged(nameof(Categories));
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(IsEmpty));
            // The pickers were refilled, so re-announce their values.
            OnPropertyChanged(nameof(Filter));
            OnPropertyChanged(nameof(CategoryFilter));
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private TaskRow MakeRow(TaskItem t, DateTime now)
    {
        var parts = new List<string>();
        if (t.Due is { } due)
        {
            var d = due.Date;
            var today = now.Date;
            string day = d == today ? this["task_today"]
                : d == today.AddDays(1) ? this["task_tomorrow"]
                : d == today.AddDays(-1) ? this["task_yesterday"]
                : DateNames.NumericDate(d, S.CalendarKind);
            if (t.DueHasTime) day += " " + due.ToString("HH:mm", CultureInfo.InvariantCulture);
            parts.Add(day);
        }

        if (t.Category.Trim().Length > 0) parts.Add(t.Category.Trim());
        var overdue = TaskFilters.IsOverdue(t, now);
        if (overdue) parts.Insert(0, this["task_overdue"]);

        return new TaskRow(t, Num(string.Join(" · ", parts)), overdue)
        {
            Title = string.IsNullOrWhiteSpace(t.Title) ? this["task_untitled"] : t.Title,
        };
    }

    private void LoadEditor()
    {
        var t = _selected?.Item;
        _loading = true;
        try
        {
            EditTitle = t?.Title ?? "";
            EditNote = t?.Note ?? "";
            EditCategory = t?.Category ?? "";
            EditPriority = t?.Priority ?? TaskPriority.Normal;
            EditDate = t?.Due;
            EditHasTime = t?.DueHasTime ?? false;
            EditHour = t?.Due is { } d && t.DueHasTime ? d.Hour : 9;
            EditMinute = t?.Due is { } d2 && t.DueHasTime ? d2.Minute : 0;
            EditReminder = t?.ReminderOffsetMinutes ?? -1;
        }
        finally
        {
            _loading = false;
        }

        OnPropertyChanged(nameof(DoneInfo));
        OnPropertyChanged(nameof(MetaText));
    }

    /// <summary>Writes the editor values of the selected task to the database. Called when switching, on Save and on close.</summary>
    public void SaveEdits()
    {
        if (_selected is null || _store is null || _loading) return;
        var t = _selected.Item;

        var due = _editDate is { } day
            ? (_editHasTime ? day.AddHours(_editHour).AddMinutes(_editMinute) : day)
            : (DateTime?)null;
        var offset = due is null || _editReminder < 0 ? (int?)null : _editReminder;

        var changed = t.Title != _editTitle.Trim() || t.Note != _editNote || t.Category != _editCategory.Trim()
            || t.Priority != _editPriority || t.Due != due || t.DueHasTime != (due is not null && _editHasTime)
            || t.ReminderOffsetMinutes != offset;
        if (!changed) return;

        try
        {
            var scheduleChanged = t.Due != due || t.DueHasTime != _editHasTime || t.ReminderOffsetMinutes != offset;
            t.Title = _editTitle.Trim();
            t.Note = _editNote;
            t.Category = _editCategory.Trim();
            t.Priority = _editPriority;
            t.Due = due;
            t.DueHasTime = due is not null && _editHasTime;
            t.ModifiedUtc = DateTime.UtcNow;
            _store.Save(t);

            if (scheduleChanged)
            {
                t.ReminderOffsetMinutes = offset;
                t.ReminderUtc = TaskFilters.ComputeReminderUtc(t);
                t.ReminderFired = false;
                _store.SetReminderSchedule(t.Id, offset, t.ReminderUtc);
            }

            Status = this["task_saved"];
        }
        catch (Exception ex)
        {
            Logger.Error("Saving a task failed.", ex);
            Status = this["task_save_error"];
        }
    }

    /// <summary>Save button / window closing: save, then refresh the list.</summary>
    public void Commit()
    {
        SaveEdits();
        Rebuild();
    }

    private void Add()
    {
        var title = _newTitle.Trim();
        if (title.Length == 0 || _store is null) return;

        try
        {
            var t = new TaskItem { Title = title };
            // New tasks added from the Today list get today's date so they stay visible.
            if (_filter == TaskFilter.Today) t.Due = DateTime.Today;
            _store.Save(t);
            _all.Add(t);
            NewTitle = "";
            Status = this["task_saved"];
            _selected = null;
            Rebuild();
        }
        catch (Exception ex)
        {
            Logger.Error("Adding a task failed.", ex);
            Status = this["task_save_error"];
        }
    }

    private void Toggle(TaskRow row)
    {
        if (_store is null) return;
        SaveEdits();
        try
        {
            var t = row.Item;
            t.Done = !t.Done;
            t.CompletedUtc = t.Done ? DateTime.UtcNow : null;
            t.ModifiedUtc = DateTime.UtcNow;
            _store.Save(t);
            if (t.Done && t.ReminderOffsetMinutes is not null)
            {
                _store.MarkReminderFired(t.Id);
                t.ReminderFired = true;
            }

            Rebuild();
            LoadEditor();
        }
        catch (Exception ex)
        {
            Logger.Error("Updating a task failed.", ex);
            Status = this["task_save_error"];
        }
    }

    private void DeleteSelected()
    {
        if (_selected is null || _store is null) return;
        if (MessageBox.Show(this["task_confirm_delete"], this["mod_tasks"], MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            var id = _selected.Id;
            _store.Delete(id);
            _all.RemoveAll(t => t.Id == id);
            _selected = null;
            Rebuild();
            LoadEditor();
            Status = this["task_deleted"];
        }
        catch (Exception ex)
        {
            Logger.Error("Deleting a task failed.", ex);
            Status = this["task_save_error"];
        }
    }

    private void ClearDone()
    {
        if (_store is null) return;
        if (!_all.Any(t => t.Done))
        {
            Status = this["task_nothing_to_clear"];
            return;
        }

        if (MessageBox.Show(this["task_confirm_clear"], this["mod_tasks"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            _store.DeleteCompleted();
            _all.RemoveAll(t => t.Done);
            if (_selected is { Done: true }) _selected = null;
            Rebuild();
            LoadEditor();
            Status = this["task_cleared"];
        }
        catch (Exception ex)
        {
            Logger.Error("Clearing tasks failed.", ex);
            Status = this["task_save_error"];
        }
    }

    /// <summary>Called by the reminder source and the controller after a snooze or announcement.</summary>
    public void Refresh()
    {
        SaveEdits();
        Reload();
        LoadEditor();
    }

    public void SelectById(string id)
    {
        Filter = TaskFilter.All;
        CategoryFilter = "";
        SearchText = "";
        var row = Rows.FirstOrDefault(r => r.Id == id);
        if (row is not null) Selected = row;
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        _priorities = null;
        _reminderChoices = null;
        Rebuild();
        base.OnSettingsChanged(sender, e);
    }

    public override void Dispose()
    {
        SaveEdits();
        base.Dispose();
    }
}
