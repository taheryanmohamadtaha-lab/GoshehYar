using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Clock;
using GoshehYar.Core.Dates;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.ViewModels;

/// <summary>One world-clock row. Values are refreshed in place every second.</summary>
public sealed class WorldClockItem : ViewModelBase
{
    private string _time = "";
    private string _hint = "";

    public WorldClockItem(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }

    public string Time
    {
        get => _time;
        set { if (_time != value) { _time = value; OnPropertyChanged(); } }
    }

    public string Hint
    {
        get => _hint;
        set { if (_hint != value) { _hint = value; OnPropertyChanged(); } }
    }
}

public sealed class ClockViewModel : LocalizedViewModel
{
    private readonly DispatcherTimer _timer;
    private readonly List<(string Id, TimeZoneInfo Zone)> _zones = new();
    private string _timeText = "";
    private string _meridiemText = "";
    private string _dateText = "";
    private double _hourAngle;
    private double _minuteAngle;
    private double _secondAngle;
    private string _status = "";
    private string? _newZoneId;
    private IReadOnlyList<OptionItem<string>>? _zoneOptions;

    public ClockViewModel(AppController controller) : base(controller)
    {
        RemoveCommand = new RelayCommand(p => Remove(p as string));
        AddCommand = new RelayCommand(AddZone);
        RebuildWorldClocks();
        Tick();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    public ICommand RemoveCommand { get; }
    public ICommand AddCommand { get; }

    public ObservableCollection<WorldClockItem> WorldClocks { get; } = new();

    public IReadOnlyList<OptionItem<CalendarKind>> Calendars =>
        new[]
        {
            new OptionItem<CalendarKind>(CalendarKind.Persian, this["clock_cal_persian"]),
            new OptionItem<CalendarKind>(CalendarKind.Gregorian, this["clock_cal_gregorian"])
        };

    /// <summary>All Windows time zones for the picker (built once).</summary>
    public IReadOnlyList<OptionItem<string>> ZoneOptions =>
        _zoneOptions ??= BuildZoneOptions();

    public string? NewZoneId
    {
        get => _newZoneId;
        set { _newZoneId = value; OnPropertyChanged(); }
    }

    public bool Use24Hour
    {
        get => S.Clock24Hour;
        set { Controller.Settings.Update(s => s.Clock24Hour = value); Tick(); }
    }

    public bool ShowSeconds
    {
        get => S.ClockShowSeconds;
        set { Controller.Settings.Update(s => s.ClockShowSeconds = value); Tick(); }
    }

    public bool ShowAnalog
    {
        get => S.ClockShowAnalog;
        set { Controller.Settings.Update(s => s.ClockShowAnalog = value); }
    }

    public CalendarKind Calendar
    {
        get => S.CalendarKind;
        set { Controller.Settings.Update(s => s.CalendarKind = value); Tick(); }
    }

    public string TimeText
    {
        get => _timeText;
        private set { if (_timeText != value) { _timeText = value; OnPropertyChanged(); } }
    }

    public string MeridiemText
    {
        get => _meridiemText;
        private set { if (_meridiemText != value) { _meridiemText = value; OnPropertyChanged(); } }
    }

    public string DateText
    {
        get => _dateText;
        private set { if (_dateText != value) { _dateText = value; OnPropertyChanged(); } }
    }

    public double HourAngle
    {
        get => _hourAngle;
        private set { _hourAngle = value; OnPropertyChanged(); }
    }

    public double MinuteAngle
    {
        get => _minuteAngle;
        private set { _minuteAngle = value; OnPropertyChanged(); }
    }

    public double SecondAngle
    {
        get => _secondAngle;
        private set { _secondAngle = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public bool HasNoZones => WorldClocks.Count == 0;

    private static IReadOnlyList<OptionItem<string>> BuildZoneOptions()
    {
        var now = DateTime.UtcNow;
        try
        {
            return TimeZoneInfo.GetSystemTimeZones()
                .Select(z => new OptionItem<string>(z.Id, WorldClock.PickerLabel(z, now)))
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Error("Clock: listing time zones failed", ex);
            return new List<OptionItem<string>>();
        }
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        base.OnSettingsChanged(sender, e);
        // Language/digits may have changed: refresh texts.
        Tick();
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var utc = DateTime.UtcNow;
        var use24 = S.Clock24Hour;
        var seconds = S.ClockShowSeconds;

        TimeText = Num(ClockFormatter.FormatTime(now, use24, seconds));
        MeridiemText = ClockFormatter.Meridiem(now, use24, S.Language);
        DateText = Num(DateNames.WeekdayName(now.DayOfWeek, S.Language) + (S.Language == AppLanguage.Persian ? "، " : ", ")
                       + DateNames.LongDate(now, S.CalendarKind, S.Language));
        HourAngle = ClockFormatter.HourAngle(now);
        MinuteAngle = ClockFormatter.MinuteAngle(now);
        SecondAngle = ClockFormatter.SecondAngle(now);

        // Keep the rows in step with the saved list (e.g. changed elsewhere).
        if (!WorldClocks.Select(w => w.Id).SequenceEqual(S.WorldClockZones.Where(z => WorldClock.Resolve(z) != null)))
        {
            RebuildWorldClocks();
        }

        var local = TimeZoneInfo.Local;
        for (var i = 0; i < WorldClocks.Count && i < _zones.Count; i++)
        {
            var zone = _zones[i].Zone;
            var zoneTime = WorldClock.TimeIn(zone, utc);
            WorldClocks[i].Time = Num(ClockFormatter.Format(zoneTime, use24, false, S.Language));
            WorldClocks[i].Hint = BuildHint(zone, local, utc);
        }
    }

    private string BuildHint(TimeZoneInfo zone, TimeZoneInfo local, DateTime utc)
    {
        var day = WorldClock.DayOffset(zone, local, utc);
        var diff = WorldClock.DifferenceText(zone, local, utc);
        var dayText = day > 0 ? this["clock_tomorrow"] : day < 0 ? this["clock_yesterday"] : this["clock_today"];
        var diffText = diff == "0" ? this["clock_same_time"] : Fmt("clock_diff_hours", Num(diff));
        return dayText + " · " + diffText;
    }

    private void RebuildWorldClocks()
    {
        _zones.Clear();
        WorldClocks.Clear();
        foreach (var id in S.WorldClockZones)
        {
            var zone = WorldClock.Resolve(id);
            if (zone == null) continue;
            _zones.Add((id, zone));
            WorldClocks.Add(new WorldClockItem(id, WorldClock.CityName(zone)));
        }

        OnPropertyChanged(nameof(HasNoZones));
    }

    private void AddZone()
    {
        var id = NewZoneId;
        if (string.IsNullOrWhiteSpace(id)) { Status = this["clock_pick_zone"]; return; }
        if (WorldClock.Resolve(id) == null) { Status = this["clock_err_unknown"]; return; }
        if (S.WorldClockZones.Contains(id)) { Status = this["clock_err_dup"]; return; }
        if (S.WorldClockZones.Count >= WorldClock.MaxZones) { Status = Fmt("clock_err_max", WorldClock.MaxZones); return; }

        Controller.Settings.Update(s => s.WorldClockZones.Add(id));
        RebuildWorldClocks();
        Tick();
        Status = this["clock_added"];
    }

    private void Remove(string? id)
    {
        if (id == null) return;
        Controller.Settings.Update(s => s.WorldClockZones.RemoveAll(z => z == id));
        RebuildWorldClocks();
        Tick();
        Status = "";
    }

    public override void Dispose()
    {
        _timer.Stop();
        base.Dispose();
    }
}
