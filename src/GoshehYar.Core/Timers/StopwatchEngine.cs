namespace GoshehYar.Core.Timers;

public sealed record LapInfo(int Number, TimeSpan LapTime, TimeSpan Total);

public sealed class StopwatchEngine
{
    private readonly Func<DateTime> _utcNow;
    private readonly List<LapInfo> _laps = new();
    private DateTime _startedUtc;
    private TimeSpan _accumulated = TimeSpan.Zero;
    private TimeSpan _lastLapTotal = TimeSpan.Zero;

    public StopwatchEngine(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
    }

    public bool IsRunning { get; private set; }

    public IReadOnlyList<LapInfo> Laps => _laps;

    public TimeSpan Elapsed
    {
        get
        {
            if (!IsRunning) return _accumulated;
            var running = _utcNow() - _startedUtc;
            if (running < TimeSpan.Zero) running = TimeSpan.Zero;
            return _accumulated + running;
        }
    }

    public bool HasData => IsRunning || _accumulated > TimeSpan.Zero || _laps.Count > 0;

    public void Start()
    {
        if (IsRunning) return;
        _startedUtc = _utcNow();
        IsRunning = true;
    }

    public void Pause()
    {
        if (!IsRunning) return;
        _accumulated = Elapsed;
        IsRunning = false;
    }

    public void Reset()
    {
        IsRunning = false;
        _accumulated = TimeSpan.Zero;
        _lastLapTotal = TimeSpan.Zero;
        _laps.Clear();
    }

    /// <summary>Records a lap (only while running). Newest lap is last in <see cref="Laps"/>.</summary>
    public LapInfo? Lap()
    {
        if (!IsRunning) return null;
        var total = Elapsed;
        var lap = new LapInfo(_laps.Count + 1, total - _lastLapTotal, total);
        _lastLapTotal = total;
        _laps.Add(lap);
        return lap;
    }
}
