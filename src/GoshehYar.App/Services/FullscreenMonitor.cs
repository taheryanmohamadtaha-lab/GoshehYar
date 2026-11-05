using System;
using System.Windows.Threading;
using GoshehYar.App.Interop;

namespace GoshehYar.App.Services;

/// <summary>
/// Detects full-screen apps, games and presentations via the Windows shell (SHQueryUserNotificationState).
/// Polls only every few seconds and only while the "stay out of the way" option is on.
/// </summary>
public sealed class FullscreenMonitor : IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _isFullscreen;

    public FullscreenMonitor()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Check();
    }

    public bool IsFullscreen => _isFullscreen;

    public event Action<bool>? Changed;

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!_timer.IsEnabled)
            {
                Check();
                _timer.Start();
            }
        }
        else
        {
            _timer.Stop();
            Update(false);
        }
    }

    private void Check()
    {
        var fullscreen = false;
        try
        {
            // 2 = busy (full-screen app), 3 = Direct3D full-screen, 4 = presentation mode
            if (NativeMethods.SHQueryUserNotificationState(out var state) == 0)
            {
                fullscreen = state is 2 or 3 or 4;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Full-screen detection failed: " + ex.Message);
        }

        Update(fullscreen);
    }

    private void Update(bool fullscreen)
    {
        if (fullscreen == _isFullscreen) return;
        _isFullscreen = fullscreen;
        Changed?.Invoke(fullscreen);
    }

    public void Dispose() => _timer.Stop();
}
