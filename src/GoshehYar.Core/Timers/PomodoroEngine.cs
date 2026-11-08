using System.Globalization;

namespace GoshehYar.Core.Timers;

public enum PomodoroPhase { Work, ShortBreak, LongBreak }

public enum PomodoroRunState { Stopped, Running, Paused }

public sealed record PomodoroConfig(int WorkMinutes, int ShortBreakMinutes, int LongBreakMinutes, int SessionsBeforeLongBreak, bool AutoStartNext)
{
    public static PomodoroConfig Default { get; } = new(25, 5, 15, 4, true);
}

/// <summary>Result of a phase that ended by itself.</summary>
public sealed record PomodoroTransition(PomodoroPhase Finished, PomodoroPhase Next, bool NextStarted);

public sealed class PomodoroEngine
{
    private readonly Func<DateTime> _utcNow;
    private DateTime _endUtc;
    private TimeSpan _pausedRemaining;
    private int _sessionsInCycle;

    public PomodoroEngine(Func<DateTime> utcNow, PomodoroConfig? config = null)
    {
        _utcNow = utcNow;
        Config = Sanitize(config ?? PomodoroConfig.Default);
    }

    public PomodoroConfig Config { get; private set; }

    public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Work;

    public PomodoroRunState State { get; private set; } = PomodoroRunState.Stopped;

    /// <summary>Completed work sessions of <see cref="CountDate"/>.</summary>
    public int CompletedToday { get; private set; }

    public string CountDate { get; private set; } = "";

    public bool IsActive => State == PomodoroRunState.Running;

    public static string DayKey(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void LoadCounter(string date, int count)
    {
        CountDate = date ?? "";
        CompletedToday = Math.Max(0, count);
    }

    /// <summary>Restarts the counter when the local day has changed. Returns true if it was reset.</summary>
    public bool RolloverIfNeeded(string todayKey)
    {
        if (CountDate == todayKey) return false;
        CountDate = todayKey;
        CompletedToday = 0;
        return true;
    }

    public void SetConfig(PomodoroConfig config) => Config = Sanitize(config);

    public TimeSpan PhaseLength(PomodoroPhase phase) => TimeSpan.FromMinutes(phase switch
    {
        PomodoroPhase.Work => Config.WorkMinutes,
        PomodoroPhase.ShortBreak => Config.ShortBreakMinutes,
        _ => Config.LongBreakMinutes,
    });

    public TimeSpan Remaining => State switch
    {
        PomodoroRunState.Running => Max0(_endUtc - _utcNow()),
        PomodoroRunState.Paused => _pausedRemaining,
        _ => PhaseLength(Phase),
    };

    public double Progress
    {
        get
        {
            var total = PhaseLength(Phase).TotalSeconds;
            if (total <= 0) return 0;
            return Math.Clamp(1.0 - Remaining.TotalSeconds / total, 0, 1);
        }
    }

    /// <summary>Work sessions finished in the current cycle (0..N-1), for the "dots" display.</summary>
    public int SessionsInCycle => _sessionsInCycle;

    public void Start()
    {
        if (State != PomodoroRunState.Stopped) return;
        _endUtc = _utcNow() + PhaseLength(Phase);
        State = PomodoroRunState.Running;
    }

    public void Pause()
    {
        if (State != PomodoroRunState.Running) return;
        _pausedRemaining = Remaining;
        State = PomodoroRunState.Paused;
    }

    public void Resume()
    {
        if (State != PomodoroRunState.Paused) return;
        _endUtc = _utcNow() + _pausedRemaining;
        State = PomodoroRunState.Running;
    }

    /// <summary>Back to a fresh work phase; the day counter is kept.</summary>
    public void Reset()
    {
        State = PomodoroRunState.Stopped;
        Phase = PomodoroPhase.Work;
        _sessionsInCycle = 0;
    }

    /// <summary>Jumps to the next phase without counting a session. Keeps running state (starts if stopped is NOT done).</summary>
    public void Skip()
    {
        var wasActive = State != PomodoroRunState.Stopped;
        Phase = Phase == PomodoroPhase.Work ? PomodoroPhase.ShortBreak : PomodoroPhase.Work;
        if (wasActive)
        {
            _endUtc = _utcNow() + PhaseLength(Phase);
            State = PomodoroRunState.Running;
        }
        else
        {
            State = PomodoroRunState.Stopped;
        }
    }

    /// <summary>Call once per second. Returns the transition when a phase has just ended.</summary>
    public PomodoroTransition? Tick()
    {
        if (State != PomodoroRunState.Running) return null;
        if (_endUtc - _utcNow() > TimeSpan.Zero) return null;

        var finished = Phase;
        if (finished == PomodoroPhase.Work)
        {
            CompletedToday++;
            _sessionsInCycle++;
            if (_sessionsInCycle >= Config.SessionsBeforeLongBreak)
            {
                _sessionsInCycle = 0;
                Phase = PomodoroPhase.LongBreak;
            }
            else
            {
                Phase = PomodoroPhase.ShortBreak;
            }
        }
        else
        {
            Phase = PomodoroPhase.Work;
        }

        if (Config.AutoStartNext)
        {
            _endUtc = _utcNow() + PhaseLength(Phase);
            State = PomodoroRunState.Running;
        }
        else
        {
            State = PomodoroRunState.Stopped;
        }

        return new PomodoroTransition(finished, Phase, Config.AutoStartNext);
    }

    private static TimeSpan Max0(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;

    private static PomodoroConfig Sanitize(PomodoroConfig c) => new(
        Math.Clamp(c.WorkMinutes, 1, 180),
        Math.Clamp(c.ShortBreakMinutes, 1, 60),
        Math.Clamp(c.LongBreakMinutes, 1, 120),
        Math.Clamp(c.SessionsBeforeLongBreak, 2, 12),
        c.AutoStartNext);
}
