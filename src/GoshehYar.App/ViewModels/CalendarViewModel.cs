using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.Services;
using GoshehYar.Core.Calendar;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.ViewModels;

/// <summary>One cell of the month grid.</summary>
public sealed class DayCell : ViewModelBase
{
    private bool _isSelected;

    public DayCell(DateTime date, string text, bool inMonth, bool isToday, bool isFriday, bool hasEvents, bool isSelected)
    {
        Date = date;
        Text = text;
        InMonth = inMonth;
        IsToday = isToday;
        IsFriday = isFriday;
        HasEvents = hasEvents;
        _isSelected = isSelected;
    }

    public DateTime Date { get; }
    public string Text { get; }
    public bool InMonth { get; }
    public bool IsToday { get; }
    public bool IsFriday { get; }
    public bool HasEvents { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>One line of the event list of the selected day.</summary>
public sealed class EventRow
{
    public EventRow(string id, string title, string timeText, string note, bool hasReminder, bool repeats)
    {
        Id = id;
        Title = title;
        TimeText = timeText;
        Note = note;
        HasReminder = hasReminder;
        Repeats = repeats;
    }

    public string Id { get; }
    public string Title { get; }
    public string TimeText { get; }
    public string Note { get; }
    public bool HasReminder { get; }
    public bool Repeats { get; }
}

public sealed class CalendarViewModel : LocalizedViewModel
{
    private readonly EventStore? _store;
    private List<CalendarEvent> _all = new();
    private DateTime _anchor;      // first day of the shown month
    private DateTime _selected;    // selected day
    private CalendarKind _lastKind;
    private bool _rebuilding;
    private EventRow? _selectedEvent;

    // editor
    private string? _editId;
    private DateTime _editCreatedUtc = DateTime.UtcNow;
    private string _editTitle = "";
    private string _editNote = "";
    private string _editDateText = "";
    private bool _editAllDay = true;
    private int _editHour = 9;
    private int _editMinute;
    private int _editReminder = -1;
    private EventRepeat _editRepeat = EventRepeat.None;
    private string _status = "";

    private IReadOnlyList<OptionItem<int>> _reminderOptions = Array.Empty<OptionItem<int>>();
    private IReadOnlyList<OptionItem<int>> _hourOptions = Array.Empty<OptionItem<int>>();
    private IReadOnlyList<OptionItem<int>> _minuteOptions = Array.Empty<OptionItem<int>>();
    private IReadOnlyList<OptionItem<EventRepeat>> _repeatOptions = Array.Empty<OptionItem<EventRepeat>>();
    private IReadOnlyList<OptionItem<CalendarKind>> _kindOptions = Array.Empty<OptionItem<CalendarKind>>();

    public CalendarViewModel(AppController controller) : base(controller)
    {
        try
        {
            _store = new EventStore(AppPaths.NotesDatabasePath);
        }
        catch (Exception ex)
        {
            Logger.Error("The calendar database could not be opened.", ex);
            _status = this["cal_db_error"];
        }

        _lastKind = Kind;
        _selected = DateTime.Today;
        var (y, m, _) = DateNames.Split(_selected, Kind);
        _anchor = MonthGrid.FirstOfMonth(y, m, Kind);

        PrevMonthCommand = new RelayCommand(() => Shift(-1));
        NextMonthCommand = new RelayCommand(() => Shift(1));
        PrevYearCommand = new RelayCommand(() => Shift(-12));
        NextYearCommand = new RelayCommand(() => Shift(12));
        TodayCommand = new RelayCommand(() => ShowDay(DateTime.Today, null));
        SelectDayCommand = new RelayCommand(p =>
        {
            if (p is DayCell cell) ShowDay(cell.Date, null);
        });
        NewEventCommand = new RelayCommand(() =>
        {
            LoadEditor(null);
            RebuildEvents();
        });
        SaveCommand = new RelayCommand(Save);
        DeleteCommand = new RelayCommand(Delete);

        BuildOptionLists();
        LoadData();
        LoadEditor(null);
        RebuildGrid();
        RebuildEvents();
        if (_store is null) Status = this["cal_db_error"];
    }

    private CalendarKind Kind => S.CalendarKind;

    public ObservableCollection<DayCell> Cells { get; } = new();
    public ObservableCollection<EventRow> Events { get; } = new();
    public IReadOnlyList<string> WeekdayHeaders { get; private set; } = Array.Empty<string>();

    public ICommand PrevMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand PrevYearCommand { get; }
    public ICommand NextYearCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand SelectDayCommand { get; }
    public ICommand NewEventCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }

    public IReadOnlyList<OptionItem<int>> ReminderOptions => _reminderOptions;
    public IReadOnlyList<OptionItem<int>> HourOptions => _hourOptions;
    public IReadOnlyList<OptionItem<int>> MinuteOptions => _minuteOptions;
    public IReadOnlyList<OptionItem<EventRepeat>> RepeatOptions => _repeatOptions;
    public IReadOnlyList<OptionItem<CalendarKind>> KindOptions => _kindOptions;

    // The first column is on the right in Persian (right-to-left) and on the left in English, so the arrows turn with it.
    private bool Rtl => S.Language == AppLanguage.Persian;
    public string PrevMonthGlyph => Rtl ? "\uE76C" : "\uE76B";
    public string NextMonthGlyph => Rtl ? "\uE76B" : "\uE76C";
    public string PrevYearGlyph => Rtl ? "\uE974" : "\uE973";
    public string NextYearGlyph => Rtl ? "\uE973" : "\uE974";

    public string MonthTitle { get; private set; } = "";
    public string SelectedTitle { get; private set; } = "";
    public string SelectedSubTitle { get; private set; } = "";

    public Visibility NoEventsVisibility => Events.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DeleteVisibility => _editId is null ? Visibility.Collapsed : Visibility.Visible;
    public string EditorTitle => this[_editId is null ? "cal_editor_new" : "cal_editor_edit"];
    public string DateHint => Fmt("cal_date_hint", Num(DateNames.NumericDate(DateTime.Today, Kind)));
    public bool TimeEnabled => !_editAllDay;

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public CalendarKind? KindValue
    {
        get => Kind;
        set
        {
            if (value is null || value.Value == Kind) return;
            var kind = value.Value;
            Controller.Settings.Update(s => s.CalendarKind = kind);
        }
    }

    public EventRow? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (_rebuilding || ReferenceEquals(_selectedEvent, value)) return;
            _selectedEvent = value;
            OnPropertyChanged();
            if (value is null) return;

            var model = _all.FirstOrDefault(e => e.Id == value.Id);
            if (model is not null) LoadEditor(model);
        }
    }

    public string EditTitle
    {
        get => _editTitle;
        set { _editTitle = value ?? ""; OnPropertyChanged(); }
    }

    public string EditNote
    {
        get => _editNote;
        set { _editNote = value ?? ""; OnPropertyChanged(); }
    }

    public string EditDateText
    {
        get => _editDateText;
        set { _editDateText = value ?? ""; OnPropertyChanged(); }
    }

    public bool EditAllDay
    {
        get => _editAllDay;
        set
        {
            _editAllDay = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TimeEnabled));
        }
    }

    public int? EditHour
    {
        get => _editHour;
        set
        {
            if (value is null) return;
            _editHour = Math.Clamp(value.Value, 0, 23);
            OnPropertyChanged();
        }
    }

    public int? EditMinute
    {
        get => _editMinute;
        set
        {
            if (value is null) return;
            _editMinute = Math.Clamp(value.Value, 0, 59);
            OnPropertyChanged();
        }
    }

    public int? EditReminder
    {
        get => _editReminder;
        set
        {
            if (value is null) return;
            _editReminder = value.Value;
            OnPropertyChanged();
        }
    }

    public EventRepeat? EditRepeat
    {
        get => _editRepeat;
        set
        {
            if (value is null) return;
            _editRepeat = value.Value;
            OnPropertyChanged();
        }
    }

    /// <summary>Shows the day of the event (its next occurrence) and loads it into the editor. Used by the reminder popup.</summary>
    public void SelectEvent(string id)
    {
        if (_store is null) return;

        try
        {
            LoadData();
            var model = _all.FirstOrDefault(e => e.Id == id);
            if (model is null) return;

            var day = model.NextOccurrenceOnOrAfter(DateTime.Today) ?? model.Date;
            ShowDay(day, model);
        }
        catch (Exception ex)
        {
            Logger.Error("Showing a calendar event failed.", ex);
        }
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        BuildOptionLists();

        var kind = Kind;
        if (kind != _lastKind)
        {
            // Show the date being edited in the newly chosen calendar.
            if (CalendarDateParser.TryParse(_editDateText, _lastKind, out var typed))
            {
                _editDateText = Num(DateNames.NumericDate(typed, kind));
            }

            _lastKind = kind;
            var (y, m, _) = DateNames.Split(_selected, kind);
            _anchor = MonthGrid.FirstOfMonth(y, m, kind);
        }
        else if (CalendarDateParser.TryParse(_editDateText, kind, out var same))
        {
            _editDateText = Num(DateNames.NumericDate(same, kind));
        }

        RebuildGrid();
        RebuildEvents();
        OnPropertyChanged(string.Empty);

        // Combo boxes lose their selection while their lists are replaced; bind everything once more afterwards.
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => OnPropertyChanged(string.Empty)));
    }

    private void BuildOptionLists()
    {
        _reminderOptions = new List<OptionItem<int>>
        {
            new(-1, this["cal_rem_none"]),
            new(0, this["cal_rem_at"]),
            new(5, Fmt("cal_rem_min", 5)),
            new(15, Fmt("cal_rem_min", 15)),
            new(30, Fmt("cal_rem_min", 30)),
            new(60, Fmt("cal_rem_hour", 1)),
            new(1440, Fmt("cal_rem_day", 1))
        };

        _hourOptions = Enumerable.Range(0, 24).Select(h => new OptionItem<int>(h, Num(h.ToString("00")))).ToList();
        _minuteOptions = Enumerable.Range(0, 60).Select(m => new OptionItem<int>(m, Num(m.ToString("00")))).ToList();

        _repeatOptions = new List<OptionItem<EventRepeat>>
        {
            new(EventRepeat.None, this["cal_rep_none"]),
            new(EventRepeat.YearlyGregorian, this["cal_rep_greg"]),
            new(EventRepeat.YearlyJalali, this["cal_rep_jalali"])
        };

        _kindOptions = new List<OptionItem<CalendarKind>>
        {
            new(CalendarKind.Persian, this["cal_kind_persian"]),
            new(CalendarKind.Gregorian, this["cal_kind_gregorian"])
        };
    }

    private void LoadData()
    {
        if (_store is null) return;

        try
        {
            _all = _store.GetAll().ToList();
        }
        catch (Exception ex)
        {
            Logger.Error("Reading calendar events failed.", ex);
            Status = this["cal_save_error"];
        }
    }

    private void Shift(int months)
    {
        var (y, m, _) = DateNames.Split(_anchor, Kind);
        var next = MonthGrid.AddMonths(y, m, months);
        try
        {
            _anchor = MonthGrid.FirstOfMonth(next.Year, next.Month, Kind);
        }
        catch (ArgumentException)
        {
            return;
        }

        RebuildGrid();
    }

    /// <summary>Selects a day, shows its month and fills the editor (with an existing event, or empty for a new one).</summary>
    private void ShowDay(DateTime day, CalendarEvent? edit)
    {
        _selected = day.Date;
        var (y, m, _) = DateNames.Split(_selected, Kind);
        _anchor = MonthGrid.FirstOfMonth(y, m, Kind);

        LoadData();
        LoadEditor(edit);
        RebuildGrid();
        RebuildEvents();
    }

    private void RebuildGrid()
    {
        var (y, m, _) = DateNames.Split(_anchor, Kind);
        var grid = MonthGrid.Build(y, m, Kind, DateTime.Today);
        var withEvents = EventStore.DaysWithEvents(_all, grid[0].Date, grid[^1].Date);

        Cells.Clear();
        foreach (var c in grid)
        {
            Cells.Add(new DayCell(
                c.Date,
                Num(c.DayNumber.ToString()),
                c.InMonth,
                c.IsToday,
                c.IsFriday && Kind == CalendarKind.Persian,
                withEvents.Contains(c.Date.Date),
                c.Date.Date == _selected.Date));
        }

        WeekdayHeaders = grid.Take(7).Select(c => DateNames.WeekdayShort(c.Date.DayOfWeek, S.Language)).ToList();
        MonthTitle = Num(DateNames.MonthName(m, Kind, S.Language) + " " + y);

        OnPropertyChanged(nameof(Cells));
        OnPropertyChanged(nameof(WeekdayHeaders));
        OnPropertyChanged(nameof(MonthTitle));
    }

    private void RebuildEvents()
    {
        _rebuilding = true;
        try
        {
            Events.Clear();
            foreach (var e in EventStore.ForDay(_all, _selected))
            {
                var time = e.AllDay
                    ? this["cal_all_day_short"]
                    : Num($"{Math.Clamp(e.TimeMinutes, 0, 1439) / 60:00}:{Math.Clamp(e.TimeMinutes, 0, 1439) % 60:00}");
                var title = string.IsNullOrWhiteSpace(e.Title) ? this["cal_untitled"] : e.Title.Trim();
                Events.Add(new EventRow(e.Id, title, time, e.Note.Trim(), e.ReminderMinutes >= 0, e.Repeat != EventRepeat.None));
            }
        }
        finally
        {
            _rebuilding = false;
        }

        _selectedEvent = _editId is null ? null : Events.FirstOrDefault(r => r.Id == _editId);

        var other = Kind == CalendarKind.Persian ? CalendarKind.Gregorian : CalendarKind.Persian;
        SelectedTitle = Fmt("cal_events_of", Num(DateNames.LongDate(_selected, Kind, S.Language)));
        SelectedSubTitle = Num(DateNames.WeekdayName(_selected.DayOfWeek, S.Language) + "، " + DateNames.LongDate(_selected, other, S.Language));

        OnPropertyChanged(nameof(SelectedEvent));
        OnPropertyChanged(nameof(SelectedTitle));
        OnPropertyChanged(nameof(SelectedSubTitle));
        OnPropertyChanged(nameof(NoEventsVisibility));
        OnPropertyChanged(nameof(DeleteVisibility));
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void LoadEditor(CalendarEvent? e)
    {
        if (e is null)
        {
            _editId = null;
            _editCreatedUtc = DateTime.UtcNow;
            _editTitle = "";
            _editNote = "";
            _editDateText = Num(DateNames.NumericDate(_selected, Kind));
            _editAllDay = true;
            _editHour = 9;
            _editMinute = 0;
            _editReminder = -1;
            _editRepeat = EventRepeat.None;
        }
        else
        {
            var minutes = Math.Clamp(e.TimeMinutes, 0, 1439);
            _editId = e.Id;
            _editCreatedUtc = e.CreatedUtc;
            _editTitle = e.Title;
            _editNote = e.Note;
            _editDateText = Num(DateNames.NumericDate(e.Date, Kind));
            _editAllDay = e.AllDay;
            _editHour = minutes / 60;
            _editMinute = minutes % 60;
            _editReminder = e.ReminderMinutes;
            _editRepeat = e.Repeat;
        }

        Status = "";
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(EditNote));
        OnPropertyChanged(nameof(EditDateText));
        OnPropertyChanged(nameof(EditAllDay));
        OnPropertyChanged(nameof(TimeEnabled));
        OnPropertyChanged(nameof(EditHour));
        OnPropertyChanged(nameof(EditMinute));
        OnPropertyChanged(nameof(EditReminder));
        OnPropertyChanged(nameof(EditRepeat));
        OnPropertyChanged(nameof(DeleteVisibility));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(DateHint));
    }

    private void Save()
    {
        if (_store is null)
        {
            Status = this["cal_db_error"];
            return;
        }

        var title = _editTitle.Trim();
        if (title.Length == 0)
        {
            Status = this["cal_err_title"];
            return;
        }

        if (!CalendarDateParser.TryParse(_editDateText, Kind, out var date))
        {
            Status = this["cal_err_date"];
            return;
        }

        var ev = new CalendarEvent
        {
            Id = _editId ?? Guid.NewGuid().ToString("N"),
            CreatedUtc = _editCreatedUtc,
            Title = title,
            Note = _editNote.Trim(),
            Date = date,
            AllDay = _editAllDay,
            TimeMinutes = _editHour * 60 + _editMinute,
            ReminderMinutes = _editReminder,
            Repeat = _editRepeat
        };

        try
        {
            _store.SaveAndSchedule(ev, DateTime.Now);
        }
        catch (Exception ex)
        {
            Logger.Error("Saving a calendar event failed.", ex);
            Status = this["cal_save_error"];
            return;
        }

        _editId = ev.Id;
        _selected = date.Date;
        var (y, m, _) = DateNames.Split(_selected, Kind);
        _anchor = MonthGrid.FirstOfMonth(y, m, Kind);

        LoadData();
        RebuildGrid();
        RebuildEvents();
        Status = this["cal_saved"];
    }

    private void Delete()
    {
        if (_store is null || _editId is null) return;

        var answer = System.Windows.MessageBox.Show(
            this["cal_delete_confirm"],
            this["mod_calendar"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (answer != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            _store.Delete(_editId);
        }
        catch (Exception ex)
        {
            Logger.Error("Deleting a calendar event failed.", ex);
            Status = this["cal_save_error"];
            return;
        }

        LoadData();
        LoadEditor(null);
        RebuildGrid();
        RebuildEvents();
        Status = this["cal_deleted"];
    }
}
