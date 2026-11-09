using GoshehYar.Core.Timers;

namespace GoshehYar.Tests;

public class TimerTests
{
    private sealed class Clock
    {
        public DateTime Now = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);
        public DateTime Get() => Now;
        public void Advance(TimeSpan t) => Now += t;
    }

    [Fact]
    public void CountdownRunsAndFinishesOnce()
    {
        var clock = new Clock();
        var c = new CountdownEngine(clock.Get);
        Assert.True(c.Start(TimeSpan.FromSeconds(10)));
        Assert.Equal(TimerState.Running, c.State);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(TimeSpan.FromSeconds(6), c.Remaining);
        Assert.Equal(0.4, c.Progress, 6);
        Assert.False(c.Tick());
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.True(c.Tick());
        Assert.False(c.Tick());
        Assert.Equal(TimerState.Finished, c.State);
        Assert.Equal(TimeSpan.Zero, c.Remaining);
    }

    [Fact]
    public void CountdownPauseKeepsRemaining()
    {
        var clock = new Clock();
        var c = new CountdownEngine(clock.Get);
        c.Start(TimeSpan.FromMinutes(1));
        clock.Advance(TimeSpan.FromSeconds(20));
        c.Pause();
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(40), c.Remaining);
        Assert.False(c.Tick());
        c.Resume();
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(TimeSpan.FromSeconds(30), c.Remaining);
    }

    [Fact]
    public void CountdownLateTickStillFinishesAndResetWorks()
    {
        var clock = new Clock();
        var c = new CountdownEngine(clock.Get);
        c.Start(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromHours(1)); // e.g. computer slept
        Assert.Equal(TimeSpan.Zero, c.Remaining);
        Assert.True(c.Tick());
        c.Reset();
        Assert.Equal(TimerState.Idle, c.State);
        Assert.Equal(TimeSpan.FromSeconds(5), c.Remaining);
    }

    [Fact]
    public void CountdownRejectsZeroAndSetTotalOnlyWhenIdle()
    {
        var clock = new Clock();
        var c = new CountdownEngine(clock.Get);
        Assert.False(c.Start(TimeSpan.Zero));
        c.SetTotal(TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(30), c.Remaining);
        c.Start(TimeSpan.FromSeconds(30));
        c.SetTotal(TimeSpan.FromSeconds(99));
        Assert.Equal(TimeSpan.FromSeconds(30), c.Total);
    }

    [Fact]
    public void StopwatchAccumulatesAcrossPauses()
    {
        var clock = new Clock();
        var s = new StopwatchEngine(clock.Get);
        s.Start();
        clock.Advance(TimeSpan.FromSeconds(3));
        s.Pause();
        clock.Advance(TimeSpan.FromSeconds(100));
        Assert.Equal(TimeSpan.FromSeconds(3), s.Elapsed);
        s.Start();
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(5), s.Elapsed);
    }

    [Fact]
    public void StopwatchLapsAndReset()
    {
        var clock = new Clock();
        var s = new StopwatchEngine(clock.Get);
        Assert.Null(s.Lap());
        s.Start();
        clock.Advance(TimeSpan.FromSeconds(10));
        var l1 = s.Lap()!;
        clock.Advance(TimeSpan.FromSeconds(7));
        var l2 = s.Lap()!;
        Assert.Equal(1, l1.Number);
        Assert.Equal(TimeSpan.FromSeconds(10), l1.LapTime);
        Assert.Equal(2, l2.Number);
        Assert.Equal(TimeSpan.FromSeconds(7), l2.LapTime);
        Assert.Equal(TimeSpan.FromSeconds(17), l2.Total);
        s.Reset();
        Assert.Empty(s.Laps);
        Assert.Equal(TimeSpan.Zero, s.Elapsed);
        Assert.False(s.HasData);
    }

    private static PomodoroEngine NewPomodoro(Clock clock, bool auto = true) =>
        new(clock.Get, new PomodoroConfig(25, 5, 15, 4, auto));

    [Fact]
    public void PomodoroWorkThenShortBreak()
    {
        var clock = new Clock();
        var p = NewPomodoro(clock);
        p.LoadCounter("2026-11-08", 0);
        p.Start();
        Assert.Equal(TimeSpan.FromMinutes(25), p.Remaining);
        clock.Advance(TimeSpan.FromMinutes(25));
        var t = p.Tick();
        Assert.NotNull(t);
        Assert.Equal(PomodoroPhase.Work, t!.Finished);
        Assert.Equal(PomodoroPhase.ShortBreak, t.Next);
        Assert.True(t.NextStarted);
        Assert.Equal(1, p.CompletedToday);
        Assert.Equal(PomodoroRunState.Running, p.State);
        Assert.Equal(TimeSpan.FromMinutes(5), p.Remaining);
    }

    [Fact]
    public void PomodoroLongBreakAfterFourSessions()
    {
        var clock = new Clock();
        var p = NewPomodoro(clock);
        p.Start();
        for (var i = 1; i <= 4; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(25));
            var t = p.Tick()!;
            Assert.Equal(i < 4 ? PomodoroPhase.ShortBreak : PomodoroPhase.LongBreak, t.Next);
            if (i < 4)
            {
                clock.Advance(TimeSpan.FromMinutes(5));
                Assert.Equal(PomodoroPhase.Work, p.Tick()!.Next);
            }
        }

        Assert.Equal(4, p.CompletedToday);
        Assert.Equal(0, p.SessionsInCycle);
        Assert.Equal(TimeSpan.FromMinutes(15), p.Remaining);
    }

    [Fact]
    public void PomodoroWithoutAutoStartStops()
    {
        var clock = new Clock();
        var p = NewPomodoro(clock, auto: false);
        p.Start();
        clock.Advance(TimeSpan.FromMinutes(25));
        var t = p.Tick()!;
        Assert.False(t.NextStarted);
        Assert.Equal(PomodoroRunState.Stopped, p.State);
        Assert.Equal(PomodoroPhase.ShortBreak, p.Phase);
        Assert.Null(p.Tick());
    }

    [Fact]
    public void PomodoroSkipDoesNotCountSession()
    {
        var clock = new Clock();
        var p = NewPomodoro(clock);
        p.Start();
        clock.Advance(TimeSpan.FromMinutes(3));
        p.Skip();
        Assert.Equal(PomodoroPhase.ShortBreak, p.Phase);
        Assert.Equal(0, p.CompletedToday);
        Assert.Equal(TimeSpan.FromMinutes(5), p.Remaining);
    }

    [Fact]
    public void PomodoroPauseResumeAndReset()
    {
        var clock = new Clock();
        var p = NewPomodoro(clock);
        p.LoadCounter("2026-11-08", 3);
        p.Start();
        clock.Advance(TimeSpan.FromMinutes(10));
        p.Pause();
        clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(TimeSpan.FromMinutes(15), p.Remaining);
        Assert.Null(p.Tick());
        p.Resume();
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(10), p.Remaining);
        p.Reset();
        Assert.Equal(PomodoroRunState.Stopped, p.State);
        Assert.Equal(PomodoroPhase.Work, p.Phase);
        Assert.Equal(3, p.CompletedToday);
    }

    [Fact]
    public void PomodoroDayRollover()
    {
        var p = NewPomodoro(new Clock());
        p.LoadCounter("2026-11-07", 5);
        Assert.False(p.RolloverIfNeeded("2026-11-07"));
        Assert.Equal(5, p.CompletedToday);
        Assert.True(p.RolloverIfNeeded("2026-11-08"));
        Assert.Equal(0, p.CompletedToday);
        Assert.Equal("2026-11-08", p.CountDate);
        Assert.Equal("2026-11-08", PomodoroEngine.DayKey(new DateTime(2026, 11, 8, 23, 59, 0)));
    }

    [Fact]
    public void PomodoroConfigIsSanitized()
    {
        var p = new PomodoroEngine(() => DateTime.UtcNow, new PomodoroConfig(0, 999, -5, 1, true));
        Assert.Equal(1, p.Config.WorkMinutes);
        Assert.Equal(60, p.Config.ShortBreakMinutes);
        Assert.Equal(1, p.Config.LongBreakMinutes);
        Assert.Equal(2, p.Config.SessionsBeforeLongBreak);
    }
}
