using System;
using System.Windows.Threading;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Services;

/// <summary>
/// Holds the live settings, notifies listeners on every change and saves to disk shortly afterwards
/// (debounced, so dragging a slider does not hammer the disk).
/// </summary>
public sealed class SettingsService
{
    private readonly SettingsStore _store;
    private readonly DispatcherTimer _saveTimer;

    public SettingsService(SettingsStore store)
    {
        _store = store;
        Current = store.Load();

        if (store.LastLoadError is not null)
        {
            Logger.Warn(store.LastLoadError + " - defaults were used.");
        }

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow();
        };
    }

    public AppSettings Current { get; private set; }

    public event EventHandler? Changed;

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Current.Normalize();
        RaiseChanged();
    }

    public void ResetToDefaults()
    {
        Current = new AppSettings();
        Current.Normalize();
        RaiseChanged();
    }

    /// <summary>Writes pending changes immediately (called on exit / restart).</summary>
    public void Flush()
    {
        if (_saveTimer.IsEnabled)
        {
            _saveTimer.Stop();
            SaveNow();
        }
    }

    private void RaiseChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not save settings.", ex);
        }
    }
}
