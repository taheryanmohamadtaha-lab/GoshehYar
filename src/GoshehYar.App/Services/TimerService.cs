using System;
using System.Windows.Threading;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Timers;

namespace GoshehYar.App.Services;

/// <summary>
/// App-level timers (countdown, stopwatch, Pomodoro). They keep running when the Timer window is closed.
/// One 1-second DispatcherTimer runs only while something is active; all timing uses absolute UTC timestamps.
/// </summary>
public sealed class TimerService : IDisposable
{
    private readonly AppController _controller;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public TimerService(AppController controller)
    {
        _controller = controller;
        Countdown = new CountdownEngine(() => DateTime.UtcNow);
        Stopwatch = new StopwatchEngine(() => DateTime.UtcNow);
        Pomodoro = new PomodoroEngine(() => DateTime.UtcNow, ReadConfig());

        var s = controller.Settings.Current;
        Countdown.SetTotal(TimeSpan.FromSeconds(Math.Max(1, s.CountdownLastSeconds)));
        Pomodoro.LoadCounter(s.PomodoroCountDate, s.PomodoroCountToday);
        EnsureToday();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        controller.Settings.Changed += OnSettingsChanged;
    }

    public CountdownEngine Countdown { get; }

    public StopwatchEngine Stopwatch { get; }

    public PomodoroEngine Pomodoro { get; }

    /// <summary>Raised on every second while something runs, and after every command.</summary>
    public event EventHandler? Changed;

    public bool AnyActive => Countdown.IsActive || Stopwatch.IsRunning || Pomodoro.IsActive;

    /// <summary>Completed Pomodoro sessions today (restarts on a new local day).</summary>
    public int CompletedToday
    {
        get
        {
            EnsureToday();
            return Pomodoro.CompletedToday;
        }
    }

    // ----- countdown -----

    public bool StartCountdown(TimeSpan length)
    {
        if (!Countdown.Start(length)) return false;
        var seconds = (int)Math.Round(length.TotalSeconds);
        _controller.Settings.Update(s => s.CountdownLastSeconds = seconds);
        Refresh();
        return true;
    }

    public void PauseCountdown() { Countdown.Pause(); Refresh(); }

    public void ResumeCountdown() { Countdown.Resume(); Refresh(); }

    public void ResetCountdown() { Countdown.Reset(); Refresh(); }

    /// <summary>Changes the length while the countdown is not running (idle or finished).</summary>
    public void SetCountdownLength(TimeSpan length)
    {
        if (Countdown.State == TimerState.Running || Countdown.State == TimerState.Paused) return;
        Countdown.SetTotal(length);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ----- stopwatch -----

    public void StartStopwatch() { Stopwatch.Start(); Refresh(); }

    public void PauseStopwatch() { Stopwatch.Pause(); Refresh(); }

    public void ResetStopwatch() { Stopwatch.Reset(); Refresh(); }

    public void LapStopwatch() { Stopwatch.Lap(); Refresh(); }

    // ----- pomodoro -----

    public void StartPomodoro()
    {
        EnsureToday();
        Pomodoro.Start();
        Refresh();
    }

    public void PausePomodoro() { Pomodoro.Pause(); Refresh(); }

    public void ResumePomodoro() { Pomodoro.Resume(); Refresh(); }

    public void ResetPomodoro() { Pomodoro.Reset(); Refresh(); }

    public void SkipPomodoro() { Pomodoro.Skip(); Refresh(); }

    /// <summary>Saves the Pomodoro settings immediately and applies them to the engine.</summary>
    public void SetPomodoroConfig(PomodoroConfig config)
    {
        Pomodoro.SetConfig(config);
        var c = Pomodoro.Config;
        _controller.Settings.Update(s =>
        {
            s.PomodoroWorkMinutes = c.WorkMinutes;
            s.PomodoroShortBreakMinutes = c.ShortBreakMinutes;
            s.PomodoroLongBreakMinutes = c.LongBreakMinutes;
            s.PomodoroSessionsBeforeLongBreak = c.SessionsBeforeLongBreak;
            s.PomodoroAutoStartNext = c.AutoStartNext;
        });
        Refresh();
    }

    public void SetSoundEnabled(bool enabled)
    {
        _controller.Settings.Update(s => s.TimerSoundEnabled = enabled);
    }

    // ----- internals -----

    private PomodoroConfig ReadConfig()
    {
        var s = _controller.Settings.Current;
        return new PomodoroConfig(s.PomodoroWorkMinutes, s.PomodoroShortBreakMinutes, s.PomodoroLongBreakMinutes,
            s.PomodoroSessionsBeforeLongBreak, s.PomodoroAutoStartNext);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Keep the engine in step if the settings were changed elsewhere (e.g. reset to defaults).
        var wanted = ReadConfig();
        if (wanted != Pomodoro.Config) Pomodoro.SetConfig(wanted);
    }

    private void EnsureToday()
    {
        var key = PomodoroEngine.DayKey(DateTime.Now);
        if (!Pomodoro.RolloverIfNeeded(key)) return;
        SaveCounter();
    }

    private void SaveCounter()
    {
        var date = Pomodoro.CountDate;
        var count = Pomodoro.CompletedToday;
        _controller.Settings.Update(s =>
        {
            s.PomodoroCountDate = date;
            s.PomodoroCountToday = count;
        });
    }

    private void Refresh()
    {
        if (_disposed) return;
        if (AnyActive)
        {
            if (!_timer.IsEnabled) _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed) return;
        try
        {
            EnsureToday();

            if (Countdown.Tick())
            {
                var body = string.Format(Text("tmr_alert_countdown_body"), DescribeLength(Countdown.Total));
                Alert(Text("tmr_alert_header"), Text("tmr_alert_countdown"), body);
            }

            var transition = Pomodoro.Tick();
            if (transition is not null)
            {
                if (transition.Finished == PomodoroPhase.Work) SaveCounter();
                var title = Text(transition.Finished == PomodoroPhase.Work ? "tmr_alert_work_done" : "tmr_alert_break_done");
                var body = Text(transition.Next switch
                {
                    PomodoroPhase.ShortBreak => "tmr_alert_next_short",
                    PomodoroPhase.LongBreak => "tmr_alert_next_long",
                    _ => "tmr_alert_next_work",
                });
                Alert(Text("tmr_alert_header"), title, body);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Timer tick failed", ex);
        }

        Refresh();
    }

    private void Alert(string header, string title, string body)
    {
        try
        {
            _controller.ShowAlert(header, title, body, _controller.Settings.Current.TimerSoundEnabled);
        }
        catch (Exception ex)
        {
            Logger.Error("Timer alert failed", ex);
        }
    }

    private string Text(string key) => Loc.Get(key, _controller.Settings.Current.Language);

    private string DescribeLength(TimeSpan t)
    {
        var total = (int)Math.Round(t.TotalSeconds);
        var h = total / 3600;
        var m = total % 3600 / 60;
        var s = total % 60;
        var text = h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
        return DigitFormatter.Format(text, _controller.Settings.Current.Digits);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _controller.Settings.Changed -= OnSettingsChanged;
    }
}
