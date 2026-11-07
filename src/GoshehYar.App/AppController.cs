using System;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using GoshehYar.App.Modules;
using GoshehYar.App.Services;
using GoshehYar.App.ViewModels;
using GoshehYar.App.Views;
using GoshehYar.Core.Backup;
using GoshehYar.Core.Input;
using GoshehYar.Core.Music;
using GoshehYar.Core.Notes;
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
    private bool _discardSettings;
    private readonly Dictionary<string, Window> _windows = new();
    private DispatcherTimer? _maintenanceTimer;
    private MediaKeyService? _mediaKeys;
    private DateTime _lastBackupAttempt = DateTime.MinValue;

    public AppController(Application application, SingleInstance singleInstance, bool startedByAutostart)
    {
        _application = application;
        _singleInstance = singleInstance;
        _startedByAutostart = startedByAutostart;

        Settings = new SettingsService(new SettingsStore(AppPaths.DataDirectory));

        try
        {
            Notes = new NoteStore(AppPaths.NotesDatabasePath);
        }
        catch (Exception ex)
        {
            Logger.Error("Notes database could not be opened; notes are unavailable.", ex);
        }

        Backups = new BackupService(AppPaths.DataDirectory, AppPaths.RestoreSafetyDirectory, () => Notes);
        Recorder = new AudioRecorderService();
        Music = new MusicPlayerService(new PlaylistStore(AppPaths.PlaylistsPath), () => Settings.Current, change => Settings.Update(change));

        RegisterModules();
    }

    /// <summary>Null when the database file could not be opened.</summary>
    public NoteStore? Notes { get; }

    public BackupService Backups { get; }

    public AudioRecorderService Recorder { get; }

    public MusicPlayerService Music { get; }

    /// <summary>Raised when a note reminder fires (argument: note id) so an open notes window can update.</summary>
    public event Action<string>? NoteReminderFired;

    /// <summary>Raised before the notes database is replaced or backed up, so open editors save their pending text.</summary>
    public event Action? NotesFlushRequested;

    public string AppVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    private void RegisterModules()
    {
        Modules.Register(new ModuleDefinition("screenshot", "mod_screenshot", "\uE722", 10, c => new Views.ScreenshotWindow(new ViewModels.ScreenshotViewModel(c))));
        Modules.Register(new ModuleDefinition("recorder", "mod_recorder", "\uE720", 20, c => new Views.RecorderWindow(new ViewModels.RecorderViewModel(c))));
        Modules.Register(new ModuleDefinition("music", "mod_music", "\uE8D6", 30, c => new Views.MusicWindow(new ViewModels.MusicViewModel(c))));
        Modules.Register(new ModuleDefinition("notes", "mod_notes", "\uE70B", 40, c =>
        {
            if (c.Notes is null)
            {
                c.ShowBalloon(Loc.Get("notes_unavailable", c.Settings.Current.Language));
                return null;
            }

            return new Views.NotesWindow(new ViewModels.NotesViewModel(c));
        }));
        Modules.Register(new ModuleDefinition("backup", "mod_backup", "\uE74E", 50, c => new Views.BackupWindow(new ViewModels.BackupViewModel(c))));
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

        _mediaKeys = Guard("media keys", () => new MediaKeyService(Music));
        Recorder.StateChanged += (_, _) => _tray?.SetRecording(Recorder.State != RecorderState.Idle);

        // Reminders and automatic backups: one slow timer, nothing runs while there is nothing to do.
        _maintenanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _maintenanceTimer.Tick += (_, _) => RunMaintenance();
        _maintenanceTimer.Start();

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

    /// <summary>Opens the window of a panel tool (or brings the open one to the front).</summary>
    public void OpenModule(IModule module)
    {
        if (module is not ModuleDefinition definition) return;

        try
        {
            if (_windows.TryGetValue(module.Id, out var existing))
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            var window = definition.Create(this);
            if (window is null) return;

            _windows[module.Id] = window;
            window.Closed += (_, _) => _windows.Remove(module.Id);
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            _windows.Remove(module.Id);
            Logger.Error($"Could not open tool '{module.Id}'.", ex);
            ShowBalloon(Loc.Get("err_unexpected", Settings.Current.Language));
            MessageBox.Show(
                Loc.Get("err_unexpected", Settings.Current.Language) + Environment.NewLine + Environment.NewLine +
                ex.GetType().Name + ": " + ex.Message + Environment.NewLine + Environment.NewLine +
                Path.Combine(AppPaths.LogsDirectory, "goshehyar.log"),
                "GoshehYar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void ShowBalloon(string text) => _tray?.ShowBalloon(text);

    public void RequestNotesFlush() => NotesFlushRequested?.Invoke();

    /// <summary>Closes the notes window (saving it) before the database is replaced.</summary>
    public void CloseNotesWindow()
    {
        if (_windows.TryGetValue("notes", out var w)) w.Close();
    }

    // ------------------------------------------------------ reminders & backup

    private void RunMaintenance()
    {
        try
        {
            CheckReminders();
            _ = AutoBackupIfDueAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Background maintenance failed.", ex);
        }
    }

    private void CheckReminders()
    {
        if (Notes is null) return;

        foreach (var note in Notes.GetDueReminders(DateTime.UtcNow))
        {
            note.ReminderFired = true;
            Notes.Save(note);
            NoteReminderFired?.Invoke(note.Id);

            var title = string.IsNullOrWhiteSpace(note.Title) ? Loc.Get("note_untitled", Settings.Current.Language) : note.Title;
            ShowBalloon(Loc.Get("reminder_prefix", Settings.Current.Language) + " " + title);
        }
    }

    private async Task AutoBackupIfDueAsync()
    {
        var s = Settings.Current;
        if (!s.BackupAutoEnabled) return;
        if (!BackupService.IsDue(s.LastBackupUtc, s.BackupFrequency, DateTime.UtcNow)) return;

        // After a failure wait an hour before trying again.
        if (DateTime.UtcNow - _lastBackupAttempt < TimeSpan.FromHours(1)) return;
        _lastBackupAttempt = DateTime.UtcNow;

        try
        {
            await BackupNowAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Automatic backup failed.", ex);
            ShowBalloon(Loc.Get("backup_failed", Settings.Current.Language));
        }
    }

    public string BackupFolderPath =>
        string.IsNullOrWhiteSpace(Settings.Current.BackupFolder) ? AppPaths.DefaultBackupDirectory : Settings.Current.BackupFolder;

    /// <summary>Creates a backup in the configured folder, removes old ones and remembers the time.</summary>
    public async Task<string> BackupNowAsync()
    {
        RequestNotesFlush();
        var folder = BackupFolderPath;
        var keep = Settings.Current.BackupKeepCount;
        var version = AppVersion;

        var path = await Task.Run(() =>
        {
            var created = Backups.CreateBackup(folder, version);
            Backups.Prune(folder, keep);
            return created;
        });

        Settings.Update(x => x.LastBackupUtc = DateTime.UtcNow);
        return path;
    }

    public void TogglePin() => Settings.Update(s => s.Pinned = !s.Pinned);

    public void HidePanelToStrip() => Panel.ShowStrip();

    /// <summary>Restarts the app. After a restore the old in-memory settings must not overwrite the restored file.</summary>
    public void Restart(bool discardPendingSettings = false)
    {
        _discardSettings = discardPendingSettings;
        if (!discardPendingSettings) Settings.Flush();
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
        if (!_discardSettings) Settings.Flush();
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
        if (!_discardSettings) Settings.Flush();

        _maintenanceTimer?.Stop();
        foreach (var window in new List<Window>(_windows.Values)) window.Close();
        _mediaKeys?.Dispose();
        Music.Dispose();
        Recorder.Dispose();

        _settingsWindow?.Close();
        _fullscreen?.Dispose();
        _hotkey?.Dispose();
        _tray?.Dispose();
        Panel?.Close();
    }
}
