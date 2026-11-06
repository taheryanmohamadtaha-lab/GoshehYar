using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GoshehYar.App.Services;
using GoshehYar.Core.Backup;
using GoshehYar.Core.Settings;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace GoshehYar.App.ViewModels;

public sealed record BackupItem(string Path, string Text);

public sealed class BackupViewModel : LocalizedViewModel
{
    private string _status = "";
    private bool _busy;
    private BackupItem? _selected;

    public BackupViewModel(AppController controller) : base(controller)
    {
        BackupNowCommand = new RelayCommand(() => _ = BackupNowAsync());
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        UseDefaultFolderCommand = new RelayCommand(UseDefaultFolder);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        ExportZipCommand = new RelayCommand(() => _ = ExportZipAsync());
        RestoreSelectedCommand = new RelayCommand(() => RestoreFrom(Selected?.Path));
        RestoreFileCommand = new RelayCommand(RestoreFromFile);

        RefreshList();
        Status = this["backup_ready"];
    }

    public ICommand BackupNowCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand UseDefaultFolderCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand ExportZipCommand { get; }
    public ICommand RestoreSelectedCommand { get; }
    public ICommand RestoreFileCommand { get; }

    public ObservableCollection<BackupItem> Backups { get; } = new();

    public BackupItem? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<OptionItem<BackupFrequency>> Frequencies =>
        new[]
        {
            new OptionItem<BackupFrequency>(BackupFrequency.Daily, this["backup_daily"]),
            new OptionItem<BackupFrequency>(BackupFrequency.Weekly, this["backup_weekly"])
        };

    public bool AutoEnabled
    {
        get => S.BackupAutoEnabled;
        set { Controller.Settings.Update(s => s.BackupAutoEnabled = value); OnPropertyChanged(); }
    }

    public BackupFrequency Frequency
    {
        get => S.BackupFrequency;
        set { Controller.Settings.Update(s => s.BackupFrequency = value); OnPropertyChanged(); }
    }

    public double KeepCount
    {
        get => S.BackupKeepCount;
        set
        {
            Controller.Settings.Update(s => s.BackupKeepCount = (int)Math.Round(value));
            OnPropertyChanged();
            OnPropertyChanged(nameof(KeepCountText));
        }
    }

    public string KeepCountText => Num(S.BackupKeepCount.ToString());

    public string FolderText => Controller.BackupFolderPath;

    public string LastBackupText => S.LastBackupUtc is { } t
        ? Num(t.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
        : this["backup_never"];

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public bool IsIdle => !_busy;

    // --------------------------------------------------------------- actions

    private async System.Threading.Tasks.Task BackupNowAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var path = await Controller.BackupNowAsync();
            Status = Fmt("backup_done", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Logger.Error("Backup failed.", ex);
            Status = this["backup_failed"];
        }
        finally
        {
            SetBusy(false);
            RefreshList();
            OnPropertyChanged(nameof(LastBackupText));
        }
    }

    private async System.Threading.Tasks.Task ExportZipAsync()
    {
        if (_busy) return;

        var dialog = new SaveFileDialog
        {
            FileName = "GoshehYar-Export-" + DateTime.Now.ToString("yyyyMMdd") + ".zip",
            Filter = "ZIP|*.zip",
            DefaultExt = ".zip",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;

        SetBusy(true);
        try
        {
            Controller.RequestNotesFlush();
            var target = dialog.FileName;
            var version = Controller.AppVersion;
            await System.Threading.Tasks.Task.Run(() => Controller.Backups.ExportZip(target, version));
            Status = Fmt("backup_exported", Path.GetFileName(target));
        }
        catch (Exception ex)
        {
            Logger.Error("ZIP export failed.", ex);
            Status = this["backup_failed"];
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RestoreFromFile()
    {
        var dialog = new OpenFileDialog { Filter = "ZIP|*.zip", Title = this["backup_restore_file"] };
        if (dialog.ShowDialog() == true) RestoreFrom(dialog.FileName);
    }

    private void RestoreFrom(string? path)
    {
        if (string.IsNullOrEmpty(path) || _busy) return;

        // 1) check the archive BEFORE touching anything
        if (!Controller.Backups.Verify(path, out var error))
        {
            Logger.Warn("Backup verification failed: " + error);
            Status = this["backup_invalid"];
            return;
        }

        // 2) ask the user
        var answer = MessageBox.Show(
            Fmt("backup_confirm_restore", Path.GetFileName(path)),
            this["backup_restore_title"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            S.Language == AppLanguage.Persian ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            // 3) close the editor (it saves), restore (a safety copy of the current data is kept), restart
            Controller.CloseNotesWindow();
            var safety = Controller.Backups.Restore(path);

            MessageBox.Show(
                Fmt("backup_restored", safety),
                this["backup_restore_title"],
                MessageBoxButton.OK,
                MessageBoxImage.Information,
                MessageBoxResult.OK,
                S.Language == AppLanguage.Persian ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);

            Controller.Restart(discardPendingSettings: true);
        }
        catch (Exception ex)
        {
            Logger.Error("Restore failed.", ex);
            Status = this["backup_restore_failed"];
        }
    }

    // ---------------------------------------------------------------- folder

    private void BrowseFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            SelectedPath = Controller.BackupFolderPath,
            ShowNewFolderButton = true,
            Description = this["backup_folder_hint"]
        };
        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        Controller.Settings.Update(s => s.BackupFolder = dialog.SelectedPath);
        OnPropertyChanged(nameof(FolderText));
        RefreshList();
    }

    private void UseDefaultFolder()
    {
        Controller.Settings.Update(s => s.BackupFolder = "");
        OnPropertyChanged(nameof(FolderText));
        RefreshList();
    }

    private void OpenFolder()
    {
        try
        {
            var folder = Controller.BackupFolderPath;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not open folder: " + ex.Message);
        }
    }

    private void RefreshList()
    {
        Backups.Clear();
        try
        {
            foreach (var b in Controller.Backups.List(Controller.BackupFolderPath))
            {
                var size = b.SizeBytes >= 1024 * 1024
                    ? $"{b.SizeBytes / 1024.0 / 1024.0:0.0} MB"
                    : $"{Math.Max(1, b.SizeBytes / 1024)} KB";
                Backups.Add(new BackupItem(b.Path, Num($"{b.CreatedLocal:yyyy-MM-dd HH:mm}  —  {size}")));
            }
        }
        catch (Exception ex)
        {
            // e.g. a USB drive that is not plugged in
            Logger.Warn("Could not read backup folder: " + ex.Message);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        OnPropertyChanged(nameof(IsIdle));
    }
}
