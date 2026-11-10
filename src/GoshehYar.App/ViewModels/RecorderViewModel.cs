using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Notes;
using GoshehYar.Core.Settings;
using NAudio.Wave;
using WinForms = System.Windows.Forms;

namespace GoshehYar.App.ViewModels;

public sealed class RecordingItem : ViewModelBase
{
    private bool _isPlaying;

    public RecordingItem(string path, string info)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Info = info;
    }

    public string Path { get; }
    public string Name { get; }
    public string Info { get; }

    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayGlyph)); }
    }

    public string PlayGlyph => IsPlaying ? "" : "";
}

public sealed class RecorderViewModel : LocalizedViewModel
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private string _fileName = "";
    private string _status = "";
    private string _selectedMic = "";

    private WaveOutEvent? _playOutput;
    private AudioFileReader? _playReader;
    private RecordingItem? _playingItem;

    public RecorderViewModel(AppController controller) : base(controller)
    {
        StartCommand = new RelayCommand(Start);
        PauseResumeCommand = new RelayCommand(PauseResume);
        StopCommand = new RelayCommand(() => _ = StopAsync());
        PlayCommand = new RelayCommand(p => Play(p as RecordingItem));
        DeleteCommand = new RelayCommand(p => Delete(p as RecordingItem));
        OpenFolderCommand = new RelayCommand(OpenFolder);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        RefreshMicsCommand = new RelayCommand(RefreshMics);

        RefreshMics();
        RefreshList();
        NewDefaultName();
        Status = this["rec_ready"];

        Controller.Recorder.StateChanged += OnRecorderStateChanged;
        _timer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(ElapsedText));
            OnPropertyChanged(nameof(LevelPercent));
        };
        _timer.Start();
    }

    private AudioRecorderService Recorder => Controller.Recorder;

    public ICommand StartCommand { get; }
    public ICommand PauseResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand RefreshMicsCommand { get; }

    public ObservableCollection<string> Microphones { get; } = new();
    public ObservableCollection<RecordingItem> Recordings { get; } = new();

    public IReadOnlyList<OptionItem<AudioFormat>> Formats =>
        new[]
        {
            new OptionItem<AudioFormat>(AudioFormat.Wav, "WAV"),
            new OptionItem<AudioFormat>(AudioFormat.Mp3, "MP3")
        };

    public string SelectedMic
    {
        get => _selectedMic;
        set
        {
            _selectedMic = value ?? "";
            OnPropertyChanged();
            if (_selectedMic.Length > 0) Controller.Settings.Update(s => s.MicrophoneName = _selectedMic);
        }
    }

    public bool IsMicSource
    {
        get => S.RecordingSource == RecordingSource.Microphone;
        set { if (value) SetSource(RecordingSource.Microphone); }
    }

    public bool IsSystemSource
    {
        get => S.RecordingSource == RecordingSource.SystemSound;
        set { if (value) SetSource(RecordingSource.SystemSound); }
    }

    private void SetSource(RecordingSource source)
    {
        if (S.RecordingSource == source) return;
        Controller.Settings.Update(s => s.RecordingSource = source);
        OnPropertyChanged(nameof(IsMicSource));
        OnPropertyChanged(nameof(IsSystemSource));
        OnPropertyChanged(nameof(MicPanelVisible));
    }

    /// <summary>The microphone picker is only needed for microphone recording.</summary>
    public bool MicPanelVisible => S.RecordingSource == RecordingSource.Microphone;

    public AudioFormat Format
    {
        get => S.RecordingFormat;
        set { Controller.Settings.Update(s => s.RecordingFormat = value); OnPropertyChanged(); }
    }

    public string FileName
    {
        get => _fileName;
        set { _fileName = value ?? ""; OnPropertyChanged(); }
    }

    public string FolderText => ResolveFolder();

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    public bool IsIdle => Recorder.State == RecorderState.Idle;
    public bool IsRecording => Recorder.State == RecorderState.Recording;
    public bool IsPaused => Recorder.State == RecorderState.Paused;
    public bool IsActive => !IsIdle;

    public string StateText => this[Recorder.State switch
    {
        RecorderState.Recording => "rec_state_recording",
        RecorderState.Paused => "rec_state_paused",
        _ => "rec_state_idle"
    }];

    public string PauseResumeText => this[IsPaused ? "rec_resume" : "rec_pause"];

    public string ElapsedText
    {
        get
        {
            var t = Recorder.Elapsed;
            return Num($"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}");
        }
    }

    public double LevelPercent => Math.Min(100, Recorder.Level * 100.0);

    // ---------------------------------------------------------------- actions

    private void Start()
    {
        if (!IsIdle) return;

        var source = S.RecordingSource;
        var index = 0;
        if (source == RecordingSource.Microphone)
        {
            var mics = Microphones.ToList();
            index = mics.IndexOf(SelectedMic);
            if (mics.Count == 0 || index < 0)
            {
                Status = this["rec_no_mic"];
                return;
            }
        }

        try
        {
            var folder = ResolveFolder();
            var name = NoteTools.SafeFileName(FileName, DefaultName());
            var path = UniquePath(Path.Combine(folder, name + ".wav"));
            Recorder.Start(source, index, path);
            Status = Fmt("rec_started", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Logger.Error("Could not start recording.", ex);
            Status = this[S.RecordingSource == RecordingSource.SystemSound ? "rec_error_start_system" : "rec_error_start"]
                + " (" + ex.GetType().Name + ": " + ex.Message + ")";
        }
    }

    private void PauseResume()
    {
        if (IsRecording) Recorder.Pause();
        else if (IsPaused) Recorder.Resume();
    }

    private async Task StopAsync()
    {
        if (IsIdle) return;

        try
        {
            Status = this["rec_saving"];
            var result = await Recorder.StopAsync(S.RecordingFormat);
            Status = result.ConversionError is null
                ? Fmt("rec_saved", Path.GetFileName(result.Path))
                : Fmt("rec_saved_wav_only", Path.GetFileName(result.Path));
        }
        catch (Exception ex)
        {
            Logger.Error("Stopping the recording failed.", ex);
            Status = this["rec_error_stop"] + " (" + ex.Message + ")";
        }

        RefreshList();
        NewDefaultName();
    }

    private void OnRecorderStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(PauseResumeText));
    }

    // ------------------------------------------------------------------ lists

    private void RefreshMics()
    {
        Microphones.Clear();
        foreach (var name in AudioRecorderService.GetMicrophones()) Microphones.Add(name);

        _selectedMic = Microphones.Contains(S.MicrophoneName)
            ? S.MicrophoneName
            : Microphones.FirstOrDefault() ?? "";
        OnPropertyChanged(nameof(SelectedMic));

        if (Microphones.Count == 0) Status = this["rec_no_mic"];
    }

    private void RefreshList()
    {
        Recordings.Clear();
        try
        {
            var folder = ResolveFolder();
            if (!Directory.Exists(folder)) return;

            var files = new DirectoryInfo(folder).GetFiles()
                .Where(f => f.Extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTime)
                .Take(300);

            foreach (var f in files)
            {
                var size = f.Length >= 1024 * 1024
                    ? $"{f.Length / 1024.0 / 1024.0:0.0} MB"
                    : $"{Math.Max(1, f.Length / 1024)} KB";
                Recordings.Add(new RecordingItem(f.FullName, Num($"{f.LastWriteTime:yyyy-MM-dd HH:mm} — {size}")));
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not list recordings: " + ex.Message);
        }
    }

    private string ResolveFolder() =>
        string.IsNullOrWhiteSpace(S.RecordingsFolder) ? AppPaths.DefaultRecordingsDirectory : S.RecordingsFolder;

    private static string DefaultName() => "Recording_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

    private void NewDefaultName() => FileName = DefaultName();

    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !File.Exists(Path.ChangeExtension(path, ".mp3"))) return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}.wav");
            if (!File.Exists(candidate) && !File.Exists(Path.ChangeExtension(candidate, ".mp3"))) return candidate;
        }

        return path;
    }

    // ------------------------------------------------------- play / delete

    private void Play(RecordingItem? item)
    {
        if (item is null) return;

        var wasThis = _playingItem == item;
        StopPlayback();
        if (wasThis) return;

        try
        {
            _playReader = new AudioFileReader(item.Path);
            _playOutput = new WaveOutEvent();
            _playOutput.Init(_playReader);
            _playOutput.PlaybackStopped += (_, _) =>
            {
                if (_playingItem == item) StopPlayback();
            };
            _playOutput.Play();
            _playingItem = item;
            item.IsPlaying = true;
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not play a recording: " + ex.Message);
            StopPlayback();
            Status = this["rec_error_play"];
        }
    }

    private void StopPlayback()
    {
        var item = _playingItem;
        _playingItem = null;
        if (item is not null) item.IsPlaying = false;

        try
        {
            _playOutput?.Dispose();
            _playReader?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warn("Stopping playback failed: " + ex.Message);
        }

        _playOutput = null;
        _playReader = null;
    }

    private void Delete(RecordingItem? item)
    {
        if (item is null) return;

        var answer = MessageBox.Show(
            Fmt("rec_confirm_delete", item.Name),
            this["rec_confirm_delete_title"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            S.Language == AppLanguage.Persian ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
        if (answer != MessageBoxResult.Yes) return;

        if (_playingItem == item) StopPlayback();

        try
        {
            File.Delete(item.Path);
            Recordings.Remove(item);
            Status = this["rec_deleted"];
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not delete a recording: " + ex.Message);
            Status = this["rec_error_delete"];
        }
    }

    private void OpenFolder()
    {
        try
        {
            var folder = ResolveFolder();
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
        using var dialog = new WinForms.FolderBrowserDialog { SelectedPath = ResolveFolder(), ShowNewFolderButton = true };
        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        Controller.Settings.Update(s => s.RecordingsFolder = dialog.SelectedPath);
        OnPropertyChanged(nameof(FolderText));
        RefreshList();
    }

    public override void Dispose()
    {
        _timer.Stop();
        Controller.Recorder.StateChanged -= OnRecorderStateChanged;
        StopPlayback();
        base.Dispose();
    }
}
