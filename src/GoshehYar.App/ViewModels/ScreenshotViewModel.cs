using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GoshehYar.App.Services;
using GoshehYar.App.Views;
using GoshehYar.Core.Settings;
using WinForms = System.Windows.Forms;

namespace GoshehYar.App.ViewModels;

public sealed class ScreenshotViewModel : LocalizedViewModel
{
    private Bitmap? _current;
    private DateTime _currentTime;
    private string? _savedPath;
    private BitmapSource? _preview;
    private string _status = "";
    private bool _busy;
    private string _selectedMonitor = "";
    private IntPtr _selectedWindow;

    public ScreenshotViewModel(AppController controller) : base(controller)
    {
        CaptureFullCommand = new RelayCommand(() => _ = CaptureAsync(Mode.Full));
        CaptureMonitorCommand = new RelayCommand(() => _ = CaptureAsync(Mode.Monitor));
        CaptureWindowCommand = new RelayCommand(() => _ = CaptureAsync(Mode.Window));
        CaptureRegionCommand = new RelayCommand(() => _ = CaptureAsync(Mode.Region));
        SaveCommand = new RelayCommand(SaveCurrent);
        CopyCommand = new RelayCommand(CopyCurrent);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        RefreshWindowsCommand = new RelayCommand(RefreshWindows);

        RefreshMonitors();
        RefreshWindows();
        Status = this["shot_ready"];
    }

    private enum Mode { Full, Monitor, Window, Region }

    /// <summary>Set by the window: hides/shows the tool window around a capture.</summary>
    public Action<bool>? SetWindowVisible { get; set; }

    public ICommand CaptureFullCommand { get; }
    public ICommand CaptureMonitorCommand { get; }
    public ICommand CaptureWindowCommand { get; }
    public ICommand CaptureRegionCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand RefreshWindowsCommand { get; }

    public ObservableCollection<OptionItem<string>> Monitors { get; } = new();
    public ObservableCollection<OptionItem<IntPtr>> Windows { get; } = new();

    public IReadOnlyList<OptionItem<ImageFormat>> Formats =>
        new[]
        {
            new OptionItem<ImageFormat>(ImageFormat.Png, "PNG"),
            new OptionItem<ImageFormat>(ImageFormat.Jpg, "JPG")
        };

    public string SelectedMonitor
    {
        get => _selectedMonitor;
        set { _selectedMonitor = value ?? ""; OnPropertyChanged(); }
    }

    public IntPtr SelectedWindow
    {
        get => _selectedWindow;
        set { _selectedWindow = value; OnPropertyChanged(); }
    }

    public ImageFormat Format
    {
        get => S.ScreenshotFormat;
        set { Controller.Settings.Update(s => s.ScreenshotFormat = value); OnPropertyChanged(); }
    }

    public double Delay
    {
        get => S.ScreenshotDelaySeconds;
        set
        {
            Controller.Settings.Update(s => s.ScreenshotDelaySeconds = (int)Math.Round(value));
            OnPropertyChanged();
            OnPropertyChanged(nameof(DelayText));
        }
    }

    public string DelayText => Num(S.ScreenshotDelaySeconds.ToString());

    public bool AutoSave
    {
        get => S.ScreenshotAutoSave;
        set { Controller.Settings.Update(s => s.ScreenshotAutoSave = value); OnPropertyChanged(); }
    }

    public string FolderText => ScreenshotService.ResolveFolder(S);

    public BitmapSource? Preview
    {
        get => _preview;
        private set { _preview = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasImage)); }
    }

    public bool HasImage => _current is not null;

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public string InfoText =>
        _current is null ? "" : Num($"{_current.Width}×{_current.Height}  —  {_currentTime:yyyy-MM-dd  HH:mm:ss}");

    // ------------------------------------------------------------------ lists

    private void RefreshMonitors()
    {
        Monitors.Clear();
        var index = 1;
        foreach (var m in ScreenService.GetMonitors())
        {
            Monitors.Add(new OptionItem<string>(m.DeviceName, Num($"{index}: {m.Bounds.Width:0}×{m.Bounds.Height:0}") + (m.IsPrimary ? " ★" : "")));
            index++;
        }

        SelectedMonitor = Monitors.Count > 0 ? Monitors[0].Value : "";
    }

    private void RefreshWindows()
    {
        Windows.Clear();
        foreach (var w in ScreenshotService.ListWindows())
        {
            var title = w.Title.Length > 70 ? w.Title[..70] + "…" : w.Title;
            Windows.Add(new OptionItem<IntPtr>(w.Handle, title));
        }

        SelectedWindow = Windows.Count > 0 ? Windows[0].Value : IntPtr.Zero;
    }

    // ---------------------------------------------------------------- capture

    private async Task CaptureAsync(Mode mode)
    {
        if (_busy) return;
        _busy = true;

        try
        {
            for (var i = S.ScreenshotDelaySeconds; i > 0; i--)
            {
                Status = Fmt("shot_countdown", i);
                await Task.Delay(1000);
            }

            Status = this["shot_working"];
            SetWindowVisible?.Invoke(false);

            Bitmap? bitmap = null;
            try
            {
                using (Controller.Panel.HideTemporarily())
                {
                    await Task.Delay(350); // let the screen redraw without our windows
                    bitmap = Grab(mode);
                }
            }
            finally
            {
                SetWindowVisible?.Invoke(true);
            }

            if (bitmap is null)
            {
                Status = this[mode == Mode.Region ? "shot_cancelled" : "shot_error"];
                return;
            }

            SetCurrent(bitmap);

            if (S.ScreenshotAutoSave)
            {
                SaveCurrent();
            }
            else
            {
                Status = this["shot_captured"];
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Screenshot failed.", ex);
            Status = this["shot_error"];
        }
        finally
        {
            _busy = false;
        }
    }

    private Bitmap? Grab(Mode mode)
    {
        switch (mode)
        {
            case Mode.Full:
                return ScreenshotService.CaptureVirtualScreen();

            case Mode.Monitor:
                var monitor = ScreenService.Resolve(SelectedMonitor);
                return ScreenshotService.CaptureMonitor(monitor);

            case Mode.Window:
                return SelectedWindow == IntPtr.Zero ? null : ScreenshotService.CaptureWindow(SelectedWindow);

            default:
                using (var frozen = ScreenshotService.CaptureVirtualScreen())
                {
                    var overlay = new RegionSelectWindow(frozen, S.Language);
                    return overlay.ShowDialog() == true ? overlay.Result : null;
                }
        }
    }

    private void SetCurrent(Bitmap bitmap)
    {
        _current?.Dispose();
        _current = bitmap;
        _currentTime = DateTime.Now;
        _savedPath = null;
        Preview = ScreenshotService.ToBitmapSource(bitmap);
        OnPropertyChanged(nameof(InfoText));
    }

    // ------------------------------------------------------------ save / copy

    private void SaveCurrent()
    {
        if (_current is null) return;

        try
        {
            _savedPath = ScreenshotService.Save(_current, ScreenshotService.ResolveFolder(S), S.ScreenshotFormat, _currentTime);
            Status = Fmt("shot_saved", Path.GetFileName(_savedPath));
        }
        catch (Exception ex)
        {
            Logger.Error("Saving the screenshot failed.", ex);
            Status = this["shot_save_error"];
        }
    }

    private void CopyCurrent()
    {
        if (_current is null) return;

        try
        {
            ScreenshotService.CopyToClipboard(_current);
            Status = this["shot_copied"];
        }
        catch (Exception ex)
        {
            // The clipboard can be locked by another program for a moment.
            Logger.Warn("Clipboard copy failed: " + ex.Message);
            Status = this["shot_copy_error"];
        }
    }

    private void OpenFolder()
    {
        try
        {
            var folder = ScreenshotService.ResolveFolder(S);
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not open folder: " + ex.Message);
        }
    }

    private void BrowseFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            SelectedPath = ScreenshotService.ResolveFolder(S),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            Controller.Settings.Update(s => s.ScreenshotFolder = dialog.SelectedPath);
            OnPropertyChanged(nameof(FolderText));
        }
    }

    public override void Dispose()
    {
        _current?.Dispose();
        _current = null;
        base.Dispose();
    }
}
