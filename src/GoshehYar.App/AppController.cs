using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.App.ViewModels;
using GoshehYar.App.Views;
using GoshehYar.Core.Input;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Modules;
using GoshehYar.Core.Settings;

namespace GoshehYar.App;

/// <summary>
/// Wires everything together: settings, panel, tray, hotkey, full-screen watcher, autostart.
/// Each part is isolated so a failure in one of them is logged instead of crashing the others.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Application _application;
    private readonly SingleInstance _singleInstance;
    private readonly bool _startedByAutostart;

    private SettingsWindow? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;
    private FullscreenMonitor? _fullscreen;
    private HotkeyService? _hotkey;
    private TrayService? _tray;
    private bool _disposed;

    public AppController(Application application, SingleInstance singleInstance, bool startedByAutostart)
    {
        _application = application;
        _singleInstance = singleInstance;
        _startedByAutostart = startedByAutostart;

        Settings = new SettingsService(new SettingsStore(AppPaths.DataDirectory));
    }

    public SettingsService Settings { get; }

    /// <summary>Tools shown on the panel. Empty in phase 1; modules register here from phase 2.</summary>
    public ModuleRegistry Modules { get; } = new();

    public PanelWindow Panel { get; private set; } = null!;

    public void Start()
    {
        ThemeManager.Apply(Settings.Current);
        SyncAutostart();

        Panel = new PanelWindow(this);
        Panel.ApplySettings();

        _tray = Guard("tray icon", () => new TrayService(this));
        _hotkey = Guard("hotkey", () => new HotkeyService());
        if (_hotkey is not null)
        {
            _hotkey.Pressed += () => Panel.Toggle();
            var result = SyncHotkey();
            if (result is HotkeyResult.Conflict or HotkeyResult.Invalid)
            {
                _tray?.ShowBalloon(Loc.Get("hotkey_conflict", Settings.Current.Language));
            }
        }

        _fullscreen = new FullscreenMonitor();
        _fullscreen.Changed += fullscreen => Panel.OnFullscreenChanged(fullscreen);
        _fullscreen.SetEnabled(Settings.Current.PauseInFullscreen);

        Settings.Changed += OnSettingsChanged;

        // A second launch of the program brings the panel back.
        _singleInstance.ListenForSignal(() =>
            _application.Dispatcher.BeginInvoke(new Action(() => Panel.ShowRevealed()), DispatcherPriority.Normal));

        // Autostart: begin as the thin handle. Manual start: show the panel for a moment so the user sees it.
        if (_startedByAutostart)
        {
            Panel.ShowStrip();
        }
        else
        {
            Panel.ShowRevealed(armAutoHide: true);
        }
    }

    // ------------------------------------------------------------------ commands

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        _settingsViewModel = new SettingsViewModel(this);
        _settingsWindow = new SettingsWindow(_settingsViewModel);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsViewModel?.Dispose();
            _settingsViewModel = null;
            _settingsWindow = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void TogglePin() => Settings.Update(s => s.Pinned = !s.Pinned);

    public void HidePanelToStrip() => Panel.ShowStrip();

    public void Restart()
    {
        Settings.Flush();
        var exe = Environment.ProcessPath;
        _singleInstance.Release();

        if (!string.IsNullOrEmpty(exe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe, "--restarted") { UseShellExecute = false });
            }
            catch (Exception ex)
            {
                Logger.Error("Restart failed.", ex);
            }
        }

        _application.Shutdown();
    }

    public void Exit()
    {
        Settings.Flush();
        _application.Shutdown();
    }

    // -------------------------------------------------------------------- hotkey

    /// <summary>Makes the registered hotkey match the settings and reports what happened.</summary>
    public HotkeyResult SyncHotkey()
    {
        if (_hotkey is null) return HotkeyResult.Invalid;

        var s = Settings.Current;
        if (!s.HotkeyEnabled)
        {
            _hotkey.Unregister();
            return HotkeyResult.Disabled;
        }

        if (!HotkeyGesture.TryParse(s.Hotkey, out var gesture))
        {
            _hotkey.Unregister();
            return HotkeyResult.Invalid;
        }

        return _hotkey.Register(gesture);
    }

    // ----------------------------------------------------------------- internals

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        try
        {
            var s = Settings.Current;
            ThemeManager.Apply(s);
            Panel.ApplySettings();
            _tray?.Refresh();
            _fullscreen?.SetEnabled(s.PauseInFullscreen);
            SyncAutostart();
        }
        catch (Exception ex)
        {
            Logger.Error("Applying settings failed.", ex);
        }
    }

    private bool _autostartApplied;
    private bool _autostartValue;

    private void SyncAutostart()
    {
        var wanted = Settings.Current.RunAtStartup;
        if (_autostartApplied && _autostartValue == wanted) return;

        _autostartApplied = true;
        _autostartValue = wanted;

        // Re-writing the value also refreshes the path after the app was moved or reinstalled.
        if (wanted || AutostartService.IsEnabled())
        {
            AutostartService.Set(wanted);
        }
    }

    private static T? Guard<T>(string what, Func<T> create) where T : class
    {
        try
        {
            return create();
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not start {what}; the rest of the app continues.", ex);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Settings.Changed -= OnSettingsChanged;
        Settings.Flush();

        _settingsWindow?.Close();
        _fullscreen?.Dispose();
        _hotkey?.Dispose();
        _tray?.Dispose();
        Panel?.Close();
    }
}
