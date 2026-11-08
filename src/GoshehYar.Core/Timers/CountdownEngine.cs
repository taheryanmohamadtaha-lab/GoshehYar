namespace GoshehYar.Core.Timers;

public enum TimerState { Idle, Running, Paused, Finished }

/// <summary>Countdown based on an absolute end time, so it stays exact after sleep or UI lag.</summary>
public sealed class CountdownEngine
{
    private readonly Func<DateTime> _utcNow;
    private DateTime _endUtc;
    private TimeSpan _pausedRemaining;

    public CountdownEngine(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
    }

    public TimerState State { get; private set; } = TimerState.Idle;

    public TimeSpan Total { get; private set; } = TimeSpan.Zero;

    public bool IsActive => State == TimerState.Running;

    public TimeSpan Remaining
    {
        get
        {
            switch (State)
            {
                case TimerState.Running:
                    var left = _endUtc - _utcNow();
                    return left < TimeSpan.Zero ? TimeSpan.Zero : left;
                case TimerState.Paused:
                    return _pausedRemaining;
                case TimerState.Finished:
                    return TimeSpan.Zero;
                default:
                    return Total;
            }
        }
    }

    /// <summary>0..1 of the time already used.</summary>
    public double Progress
    {
        get
        {
            if (Total <= TimeSpan.Zero) return 0;
            var p = 1.0 - Remaining.TotalSeconds / Total.TotalSeconds;
            return Math.Clamp(p, 0, 1);
        }
    }

    /// <summary>Sets the length while idle/finished (does not start).</summary>
    public void SetTotal(TimeSpan total)
    {
        if (State == TimerState.Running || State == TimerState.Paused) return;
        Total = total < TimeSpan.Zero ? TimeSpan.Zero : total;
        State = TimerState.Idle;
    }

    public bool Start(TimeSpan total)
    {
        if (total <= TimeSpan.Zero) return false;
        Total = total;
        _endUtc = _utcNow() + total;
        State = TimerState.Running;
        return true;
    }

    public void Pause()
    {
        if (State != TimerState.Running) return;
        _pausedRemaining = Remaining;
        State = TimerState.Paused;
    }

    public void Resume()
    {
        if (State != TimerState.Paused) return;
        _endUtc = _utcNow() + _pausedRemaining;
        State = TimerState.Running;
    }

    public void Reset()
    {
        State = TimerState.Idle;
        _pausedRemaining = TimeSpan.Zero;
    }

    /// <summary>Returns true exactly once, when the countdown has just reached zero.</summary>
    public bool Tick()
    {
        if (State != TimerState.Running) return false;
        if (_endUtc - _utcNow() > TimeSpan.Zero) return false;
        State = TimerState.Finished;
        return true;
    }
}
