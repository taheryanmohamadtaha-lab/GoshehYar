using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Timers;

namespace GoshehYar.App.ViewModels;

public sealed record PresetItem(int Minutes, string Label);

public sealed record LapRow(string Label, string LapTime, string Total);

/// <summary>Countdown, stopwatch and Pomodoro. The engines live in <see cref="TimerService"/>, so they keep running when this window is closed.</summary>
public sealed class TimerViewModel : LocalizedViewModel
{
    private readonly TimerService _svc;
    private readonly DispatcherTimer _fastTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private int _hours;
    private int _minutes;
    private int _seconds;
    private int _lapCount = -1;
    private bool _disposed;

    public TimerViewModel(AppController controller) : base(controller)
    {
        _svc = controller.Timers;

        var total = _svc.Countdown.Total > TimeSpan.Zero ? (int)_svc.Countdown.Total.TotalSeconds : S.CountdownLastSeconds;
        total = Math.Max(1, total);
        _hours = Math.Min(23, total / 3600);
        _minutes = total % 3600 / 60;
        _seconds = total % 60;

        StartCountdownCommand = new RelayCommand(StartCountdown);
        PauseCountdownCommand = new RelayCommand(() => _svc.PauseCountdown());
        ResumeCountdownCommand = new RelayCommand(() => _svc.ResumeCountdown());
        ResetCountdownCommand = new RelayCommand(() => _svc.ResetCountdown());
        PresetCommand = new RelayCommand(p =>
        {
            if (p is null || !int.TryParse(Convert.ToString(p, System.Globalization.CultureInfo.InvariantCulture), out var m)) return;
            if (_svc.Countdown.State is TimerState.Running or TimerState.Paused) return;
            SetInputs(TimeSpan.FromMinutes(m));
            _svc.StartCountdown(TimeSpan.FromMinutes(m));
        });

        StartStopwatchCommand = new RelayCommand(() => _svc.StartStopwatch());
        PauseStopwatchCommand = new RelayCommand(() => _svc.PauseStopwatch());
        ResetStopwatchCommand = new RelayCommand(() => _svc.ResetStopwatch());
        LapCommand = new RelayCommand(() => _svc.LapStopwatch());

        PomodoroToggleCommand = new RelayCommand(() =>
        {
            switch (_svc.Pomodoro.State)
            {
                case PomodoroRunState.Running: _svc.PausePomodoro(); break;
                case PomodoroRunState.Paused: _svc.ResumePomodoro(); break;
                default: _svc.StartPomodoro(); break;
            }
        });
        PomodoroResetCommand = new RelayCommand(() => _svc.ResetPomodoro());
        PomodoroSkipCommand = new RelayCommand(() => _svc.SkipPomodoro());

        _fastTimer.Tick += (_, _) => RaiseStopwatch();
        _svc.Changed += OnServiceChanged;
        RebuildLaps();
        UpdateFastTimer();
    }

    public IReadOnlyList<int> HourChoices { get; } = Enumerable.Range(0, 24).ToList();
    public IReadOnlyList<int> MinuteChoices { get; } = Enumerable.Range(0, 60).ToList();
    public IReadOnlyList<int> WorkChoices { get; } = Enumerable.Range(1, 120).ToList();
    public IReadOnlyList<int> ShortChoices { get; } = Enumerable.Range(1, 30).ToList();
    public IReadOnlyList<int> LongChoices { get; } = Enumerable.Range(1, 60).ToList();
    public IReadOnlyList<int> SessionChoices { get; } = Enumerable.Range(2, 7).ToList();

    private IReadOnlyList<PresetItem>? _presets;

    public IReadOnlyList<PresetItem> Presets =>
        _presets ??= new[] { 1, 5, 10, 15, 30 }.Select(m => new PresetItem(m, Fmt("tmr_preset_min", m))).ToList();

    public ObservableCollection<LapRow> Laps { get; } = new();

    public ICommand StartCountdownCommand { get; }
    public ICommand PauseCountdownCommand { get; }
    public ICommand ResumeCountdownCommand { get; }
    public ICommand ResetCountdownCommand { get; }
    public ICommand PresetCommand { get; }
    public ICommand StartStopwatchCommand { get; }
    public ICommand PauseStopwatchCommand { get; }
    public ICommand ResetStopwatchCommand { get; }
    public ICommand LapCommand { get; }
    public ICommand PomodoroToggleCommand { get; }
    public ICommand PomodoroResetCommand { get; }
    public ICommand PomodoroSkipCommand { get; }

    // ---------------------------------------------------------------- countdown

    public int Hours
    {
        get => _hours;
        set { _hours = Math.Clamp(value, 0, 23); OnPropertyChanged(); }
    }

    public int Minutes
    {
        get => _minutes;
        set { _minutes = Math.Clamp(value, 0, 59); OnPropertyChanged(); }
    }

    public int Seconds
    {
        get => _seconds;
        set { _seconds = Math.Clamp(value, 0, 59); OnPropertyChanged(); }
    }

    private void SetInputs(TimeSpan length)
    {
        var total = (int)Math.Round(length.TotalSeconds);
        _hours = Math.Min(23, total / 3600);
        _minutes = total % 3600 / 60;
        _seconds = total % 60;
        OnPropertyChanged(nameof(Hours));
        OnPropertyChanged(nameof(Minutes));
        OnPropertyChanged(nameof(Seconds));
    }

    private void StartCountdown()
    {
        var length = new TimeSpan(_hours, _minutes, _seconds);
        if (length <= TimeSpan.Zero) return;
        _svc.StartCountdown(length);
    }

    private TimerState CdState => _svc.Countdown.State;

    public bool CdCanStart => CdState is TimerState.Idle or TimerState.Finished;
    public bool CdIsRunning => CdState == TimerState.Running;
    public bool CdIsPaused => CdState == TimerState.Paused;

    public string CdDisplay => Num(Clock(CdCanStart && CdState == TimerState.Idle
        ? new TimeSpan(_hours, _minutes, _seconds)
        : _svc.Countdown.Remaining));

    public double CdProgress => _svc.Countdown.Progress * 100.0;

    public string CdStateText => this[CdState switch
    {
        TimerState.Running => "tmr_state_running",
        TimerState.Paused => "tmr_state_paused",
        TimerState.Finished => "tmr_state_finished",
        _ => "tmr_state_idle",
    }];

    // ---------------------------------------------------------------- stopwatch

    public string StopwatchText
    {
        get
        {
            var t = _svc.Stopwatch.Elapsed;
            return Num($"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}");
        }
    }

    public bool SwIsRunning => _svc.Stopwatch.IsRunning;
    public bool SwIsStopped => !_svc.Stopwatch.IsRunning;
    public bool SwCanReset => _svc.Stopwatch.HasData;
    public bool HasLaps => Laps.Count > 0;
    public bool NoLaps => Laps.Count == 0;

    private void RebuildLaps()
    {
        var laps = _svc.Stopwatch.Laps;
        if (laps.Count == _lapCount) return;
        _lapCount = laps.Count;

        Laps.Clear();
        foreach (var lap in laps.Reverse())
        {
            Laps.Add(new LapRow(Fmt("tmr_lap_row", lap.Number), Num(Short(lap.LapTime)), Num(Short(lap.Total))));
        }

        OnPropertyChanged(nameof(HasLaps));
        OnPropertyChanged(nameof(NoLaps));
    }

    private static string Short(TimeSpan t) =>
        $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}";

    // ---------------------------------------------------------------- pomodoro

    public string PomodoroDisplay => Num(Clock(_svc.Pomodoro.Remaining));
    public double PomodoroProgress => _svc.Pomodoro.Progress * 100.0;

    public string PomodoroPhaseText => this[_svc.Pomodoro.Phase switch
    {
        PomodoroPhase.ShortBreak => "tmr_phase_short",
        PomodoroPhase.LongBreak => "tmr_phase_long",
        _ => "tmr_phase_work",
    }];

    public string PomodoroToggleText => this[_svc.Pomodoro.State switch
    {
        PomodoroRunState.Running => "tmr_pause",
        PomodoroRunState.Paused => "tmr_resume",
        _ => "tmr_start",
    }];

    public bool PomodoroCanReset => _svc.Pomodoro.State != PomodoroRunState.Stopped;

    public string PomodoroTodayText => this["tmr_today"] + ": " + Num(_svc.CompletedToday.ToString());

    public string PomodoroCycleText => Fmt("tmr_cycle", _svc.Pomodoro.SessionsInCycle, _svc.Pomodoro.Config.SessionsBeforeLongBreak);

    private void SaveConfig(Func<PomodoroConfig, PomodoroConfig> change) => _svc.SetPomodoroConfig(change(_svc.Pomodoro.Config));

    public int WorkMinutes
    {
        get => _svc.Pomodoro.Config.WorkMinutes;
        set { if (value != WorkMinutes) SaveConfig(c => c with { WorkMinutes = value }); }
    }

    public int ShortMinutes
    {
        get => _svc.Pomodoro.Config.ShortBreakMinutes;
        set { if (value != ShortMinutes) SaveConfig(c => c with { ShortBreakMinutes = value }); }
    }

    public int LongMinutes
    {
        get => _svc.Pomodoro.Config.LongBreakMinutes;
        set { if (value != LongMinutes) SaveConfig(c => c with { LongBreakMinutes = value }); }
    }

    public int SessionsBeforeLong
    {
        get => _svc.Pomodoro.Config.SessionsBeforeLongBreak;
        set { if (value != SessionsBeforeLong) SaveConfig(c => c with { SessionsBeforeLongBreak = value }); }
    }

    public bool AutoStartNext
    {
        get => _svc.Pomodoro.Config.AutoStartNext;
        set { if (value != AutoStartNext) SaveConfig(c => c with { AutoStartNext = value }); }
    }

    public bool SoundEnabled
    {
        get => S.TimerSoundEnabled;
        set { if (value != S.TimerSoundEnabled) _svc.SetSoundEnabled(value); }
    }

    // ---------------------------------------------------------------- plumbing

    private static string Clock(TimeSpan t)
    {
        var seconds = (int)Math.Ceiling(Math.Max(0, t.TotalSeconds));
        return $"{seconds / 3600:00}:{seconds % 3600 / 60:00}:{seconds % 60:00}";
    }

    private void OnServiceChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        RebuildLaps();
        UpdateFastTimer();
        foreach (var name in LiveProperties) OnPropertyChanged(name);
    }

    // Raised every second; the lists (presets, choices) are not touched so buttons stay clickable.
    private static readonly string[] LiveProperties =
    {
        nameof(Hours), nameof(Minutes), nameof(Seconds),
        nameof(CdCanStart), nameof(CdIsRunning), nameof(CdIsPaused), nameof(CdDisplay), nameof(CdProgress), nameof(CdStateText),
        nameof(StopwatchText), nameof(SwIsRunning), nameof(SwIsStopped), nameof(SwCanReset),
        nameof(PomodoroDisplay), nameof(PomodoroProgress), nameof(PomodoroPhaseText), nameof(PomodoroToggleText),
        nameof(PomodoroCanReset), nameof(PomodoroTodayText), nameof(PomodoroCycleText),
        nameof(WorkMinutes), nameof(ShortMinutes), nameof(LongMinutes), nameof(SessionsBeforeLong), nameof(AutoStartNext)
    };

    private void UpdateFastTimer()
    {
        if (_svc.Stopwatch.IsRunning) _fastTimer.Start();
        else _fastTimer.Stop();
    }

    private void RaiseStopwatch() => OnPropertyChanged(nameof(StopwatchText));

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _lapCount = -1;
        _presets = null;
        RebuildLaps();
        base.OnSettingsChanged(sender, e);
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fastTimer.Stop();
        _svc.Changed -= OnServiceChanged;
        base.Dispose();
    }
}
