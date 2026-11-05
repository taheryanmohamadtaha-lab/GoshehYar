using System;
using System.Threading;

namespace GoshehYar.App.Services;

/// <summary>
/// Makes sure only one copy runs. A second launch signals the first copy (which then shows the panel) and exits.
/// The mutex name is also used by the installer (AppMutex) to detect a running app.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const string MutexName = "GoshehYar.SingleInstance.v1";
    private const string SignalName = "GoshehYar.ShowSignal.v1";

    private Mutex? _mutex;
    private EventWaitHandle? _signal;
    private RegisteredWaitHandle? _registration;
    private bool _owns;

    /// <summary>Tries to become the one running instance; waits up to <paramref name="wait"/> (used after a restart).</summary>
    public bool TryAcquire(TimeSpan wait)
    {
        _mutex = new Mutex(false, MutexName);
        try
        {
            _owns = _mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            // The previous copy crashed; we own the mutex now.
            _owns = true;
        }

        if (_owns)
        {
            _signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        }

        return _owns;
    }

    /// <summary>Asks the already-running copy to show its panel.</summary>
    public static void SignalExisting()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(SignalName);
            signal.Set();
        }
        catch (Exception)
        {
            // The first copy is still starting up or is shutting down: nothing to do.
        }
    }

    /// <summary>Calls <paramref name="onSignal"/> (on a thread-pool thread) whenever another launch happens.</summary>
    public void ListenForSignal(Action onSignal)
    {
        if (_signal is null) return;
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _signal,
            (_, _) => onSignal(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Gives up ownership so a restarted copy can take over.</summary>
    public void Release()
    {
        _registration?.Unregister(null);
        _registration = null;

        if (_owns && _mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Released from a different thread: the OS frees it when the process exits.
            }

            _owns = false;
        }
    }

    public void Dispose()
    {
        Release();
        _signal?.Dispose();
        _signal = null;
        _mutex?.Dispose();
        _mutex = null;
    }
}
