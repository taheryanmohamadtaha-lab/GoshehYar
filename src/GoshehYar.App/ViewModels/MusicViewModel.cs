using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.App.Views;
using GoshehYar.Core.Music;
using GoshehYar.Core.Settings;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace GoshehYar.App.ViewModels;

public sealed class TrackItem : ViewModelBase
{
    private bool _isCurrent;

    public TrackItem(int index, string path, string number)
    {
        Index = index;
        Path = path;
        Number = number;
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
        Extension = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
    }

    public int Index { get; }
    public string Path { get; }
    public string Number { get; }
    public string Name { get; }
    public string Extension { get; }

    public bool IsCurrent
    {
        get => _isCurrent;
        set { _isCurrent = value; OnPropertyChanged(); }
    }
}

public sealed class MusicViewModel : LocalizedViewModel
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private double _positionSeconds;
    private bool _scrubbing;
    private bool _compact;
    private string _status = "";
    private TrackItem? _selected;

    public MusicViewModel(AppController controller) : base(controller)
    {
        PlayPauseCommand = new RelayCommand(Music.TogglePlayPause);
        NextCommand = new RelayCommand(Music.Next);
        PreviousCommand = new RelayCommand(Music.Previous);
        StopCommand = new RelayCommand(Music.Stop);
        ToggleShuffleCommand = new RelayCommand(() => Music.SetShuffle(!Music.Shuffle));
        CycleRepeatCommand = new RelayCommand(CycleRepeat);
        AddFilesCommand = new RelayCommand(AddFiles);
        AddFolderCommand = new RelayCommand(AddFolder);
        RemoveCommand = new RelayCommand(RemoveSelected);
        NewPlaylistCommand = new RelayCommand(NewPlaylist);
        RenamePlaylistCommand = new RelayCommand(RenamePlaylist);
        DeletePlaylistCommand = new RelayCommand(DeletePlaylist);
        ToggleCompactCommand = new RelayCommand(() => IsCompact = !IsCompact);

        Music.Changed += OnMusicChanged;
        Music.Problem += OnProblem;
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        Rebuild();
    }

    private MusicPlayerService Music => Controller.Music;

    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ToggleShuffleCommand { get; }
    public ICommand CycleRepeatCommand { get; }
    public ICommand AddFilesCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand NewPlaylistCommand { get; }
    public ICommand RenamePlaylistCommand { get; }
    public ICommand DeletePlaylistCommand { get; }
    public ICommand ToggleCompactCommand { get; }

    public ObservableCollection<TrackItem> Tracks { get; } = new();
    public ObservableCollection<OptionItem<string>> PlaylistOptions { get; } = new();

    public TrackItem? SelectedTrack
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); }
    }

    public string SelectedPlaylistId
    {
        get => Music.Current.Id;
        set
        {
            if (string.IsNullOrEmpty(value) || value == Music.Current.Id) return;
            Music.SelectPlaylist(value);
        }
    }

    public bool IsCompact
    {
        get => _compact;
        set { _compact = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowList)); }
    }

    public bool ShowList => !_compact;

    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    // ----------------------------------------------------------- now playing

    public string PlayGlyph => Music.IsPlaying ? "" : "";

    public bool IsShuffle => Music.Shuffle;

    public string RepeatGlyph => Music.Repeat == RepeatMode.One ? "" : "";

    public string RepeatTip => this[Music.Repeat switch
    {
        RepeatMode.One => "music_repeat_one",
        RepeatMode.All => "music_repeat_all",
        _ => "music_repeat_off"
    }];

    public bool RepeatActive => Music.Repeat != RepeatMode.None;

    public string NowTitle =>
        Music.CurrentPath is { } p ? Path.GetFileNameWithoutExtension(p) : this["music_nothing"];

    public string NowInfo
    {
        get
        {
            var path = Music.CurrentPath;
            if (path is null) return "";

            var parts = new List<string> { Path.GetExtension(path).TrimStart('.').ToUpperInvariant() };
            try
            {
                var size = new FileInfo(path).Length;
                parts.Add(size >= 1024 * 1024 ? $"{size / 1024.0 / 1024.0:0.0} MB" : $"{Math.Max(1, size / 1024)} KB");
            }
            catch (IOException)
            {
            }

            parts.Add(Path.GetFileName(Path.GetDirectoryName(path) ?? ""));
            return Num(string.Join("  •  ", parts.Where(x => x.Length > 0)));
        }
    }

    public double DurationSeconds => Math.Max(1, Music.Duration.TotalSeconds);

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            _positionSeconds = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PositionText));
        }
    }

    public string PositionText => Num(Format(TimeSpan.FromSeconds(_positionSeconds)));

    public string DurationText => Num(Format(Music.Duration));

    public double VolumePercent
    {
        get => Math.Round(Music.Volume * 100);
        set
        {
            Music.SetVolume(value / 100.0);
            OnPropertyChanged();
        }
    }

    // ------------------------------------------------------------- scrubbing

    public void BeginScrub() => _scrubbing = true;

    public void EndScrub()
    {
        if (!_scrubbing) return;
        _scrubbing = false;
        Music.Seek(TimeSpan.FromSeconds(_positionSeconds));
    }

    private void Tick()
    {
        if (!Music.HasTrack)
        {
            if (_positionSeconds != 0) PositionSeconds = 0;
            return;
        }

        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(DurationText));
        if (!_scrubbing) PositionSeconds = Music.Position.TotalSeconds;
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";

    // --------------------------------------------------------------- playlist

    public void PlayTrack(TrackItem? track)
    {
        if (track is not null) Music.PlayIndex(track.Index);
    }

    private void OnMusicChanged(object? sender, EventArgs e) => Rebuild();

    private void OnProblem(string key) => Status = this[key];

    private void Rebuild()
    {
        var playlist = Music.Current;

        PlaylistOptions.Clear();
        foreach (var p in Music.Playlists) PlaylistOptions.Add(new OptionItem<string>(p.Id, p.Name));

        var wasSelected = SelectedTrack?.Index ?? -1;
        Tracks.Clear();
        for (var i = 0; i < playlist.Files.Count; i++)
        {
            Tracks.Add(new TrackItem(i, playlist.Files[i], Num((i + 1).ToString()))
            {
                IsCurrent = i == Music.CurrentIndex
            });
        }

        if (wasSelected >= 0 && wasSelected < Tracks.Count) SelectedTrack = Tracks[wasSelected];

        OnPropertyChanged(nameof(SelectedPlaylistId));
        OnPropertyChanged(nameof(PlayGlyph));
        OnPropertyChanged(nameof(IsShuffle));
        OnPropertyChanged(nameof(RepeatGlyph));
        OnPropertyChanged(nameof(RepeatTip));
        OnPropertyChanged(nameof(RepeatActive));
        OnPropertyChanged(nameof(NowTitle));
        OnPropertyChanged(nameof(NowInfo));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(VolumePercent));
    }

    private void CycleRepeat()
    {
        Music.SetRepeat(Music.Repeat switch
        {
            RepeatMode.None => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.None
        });
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = this["music_filter"] + "|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.wma;*.ogg"
        };
        if (dialog.ShowDialog() != true) return;

        var added = Music.AddFiles(dialog.FileNames);
        Status = Fmt("music_added", added);
    }

    private void AddFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog { ShowNewFolderButton = false };
        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

        var added = Music.AddFolder(dialog.SelectedPath);
        Status = Fmt("music_added", added);
    }

    private void RemoveSelected()
    {
        if (SelectedTrack is null) return;
        Music.RemoveTrack(SelectedTrack.Index);
        SelectedTrack = null;
    }

    private void NewPlaylist()
    {
        var name = InputDialog.Ask(Application.Current.Windows.OfType<MusicWindow>().FirstOrDefault(),
            this["music_new_playlist"], this["music_playlist_name"], "", S.Language);
        if (name is not null) Music.CreatePlaylist(name);
    }

    private void RenamePlaylist()
    {
        var name = InputDialog.Ask(Application.Current.Windows.OfType<MusicWindow>().FirstOrDefault(),
            this["music_rename_playlist"], this["music_playlist_name"], Music.Current.Name, S.Language);
        if (name is not null) Music.RenamePlaylist(name);
    }

    private void DeletePlaylist()
    {
        var answer = MessageBox.Show(
            Fmt("music_confirm_delete", Music.Current.Name),
            this["music_delete_playlist"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            S.Language == AppLanguage.Persian ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
        if (answer == MessageBoxResult.Yes) Music.DeleteCurrentPlaylist();
    }

    public override void Dispose()
    {
        _timer.Stop();
        Music.Changed -= OnMusicChanged;
        Music.Problem -= OnProblem;
        base.Dispose();
    }
}
