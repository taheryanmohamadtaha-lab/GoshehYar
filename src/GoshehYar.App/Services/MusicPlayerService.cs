using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoshehYar.Core.Music;
using GoshehYar.Core.Settings;
using NAudio.Wave;

namespace GoshehYar.App.Services;

/// <summary>
/// Plays music files from the user's computer. Lives in the app so music keeps playing when its window is closed.
/// Playlists only store file paths; no music is copied.
/// </summary>
public sealed class MusicPlayerService : IDisposable
{
    private readonly PlaylistStore _store;
    private readonly Func<AppSettings> _settings;
    private readonly Action<Action<AppSettings>> _updateSettings;
    private readonly PlaybackQueue _queue = new();

    private AudioFileReader? _reader;
    private WaveOutEvent? _output;
    private bool _ignoreStop;

    public MusicPlayerService(PlaylistStore store, Func<AppSettings> settings, Action<Action<AppSettings>> updateSettings)
    {
        _store = store;
        _settings = settings;
        _updateSettings = updateSettings;

        Playlists = store.Load();
        var wanted = settings().LastPlaylistId;
        Current = Playlists.FirstOrDefault(p => p.Id == wanted) ?? Playlists[0];

        _queue.Repeat = settings().MusicRepeat;
        _queue.Reset(Current.Files.Count, settings().MusicShuffle);
    }

    public List<Playlist> Playlists { get; private set; }

    public Playlist Current { get; private set; }

    /// <summary>Raised when anything the UI shows has changed (track, play state, playlist content).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised with a short message key when a file could not be played.</summary>
    public event Action<string>? Problem;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    public bool IsPaused => _output?.PlaybackState == PlaybackState.Paused;

    /// <summary>True while a track is loaded (playing or paused).</summary>
    public bool HasTrack => _reader is not null;

    public int CurrentIndex => HasTrack ? _queue.CurrentIndex : -1;

    public string? CurrentPath => CurrentIndex >= 0 && CurrentIndex < Current.Files.Count ? Current.Files[CurrentIndex] : null;

    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public float Volume => (float)_settings().MusicVolume;

    public bool Shuffle => _settings().MusicShuffle;

    public RepeatMode Repeat => _settings().MusicRepeat;

    // ---------------------------------------------------------------- playback

    public void PlayIndex(int index)
    {
        if (index < 0 || index >= Current.Files.Count) return;
        _queue.SelectIndex(index);
        StartCurrent();
    }

    public void TogglePlayPause()
    {
        if (_output is null)
        {
            if (Current.Files.Count == 0) return;
            var index = _queue.CurrentIndex >= 0 ? _queue.CurrentIndex : _queue.Next(false);
            PlayIndex(index);
            return;
        }

        if (_output.PlaybackState == PlaybackState.Playing) _output.Pause();
        else _output.Play();

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        ReleaseOutput();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Next() => Advance(_queue.Next(false));

    public void Previous()
    {
        // A common player habit: after a few seconds "previous" restarts the current track.
        if (_reader is not null && _reader.CurrentTime > TimeSpan.FromSeconds(3))
        {
            _reader.CurrentTime = TimeSpan.Zero;
            return;
        }

        Advance(_queue.Previous());
    }

    public void Seek(TimeSpan position)
    {
        if (_reader is null) return;
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > _reader.TotalTime) position = _reader.TotalTime;
        _reader.CurrentTime = position;
    }

    public void SetVolume(double volume)
    {
        volume = Math.Min(1, Math.Max(0, volume));
        if (_reader is not null) _reader.Volume = (float)volume;
        _updateSettings(s => s.MusicVolume = volume);
    }

    public void SetShuffle(bool shuffle)
    {
        _queue.SetShuffle(shuffle);
        _updateSettings(s => s.MusicShuffle = shuffle);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetRepeat(RepeatMode mode)
    {
        _queue.Repeat = mode;
        _updateSettings(s => s.MusicRepeat = mode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Advance(int index)
    {
        if (index < 0)
        {
            Stop();
            return;
        }

        StartCurrent();
    }

    private void StartCurrent(int attempts = 0)
    {
        var index = _queue.CurrentIndex;
        if (index < 0 || index >= Current.Files.Count) return;

        var path = Current.Files[index];
        ReleaseOutput();

        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);

            var reader = new AudioFileReader(path) { Volume = Volume };
            var output = new WaveOutEvent();
            output.Init(reader);
            output.PlaybackStopped += OnPlaybackStopped;

            _reader = reader;
            _output = output;
            output.Play();
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not play a track: " + ex.Message);
            ReleaseOutput();
            Problem?.Invoke("music_error_file");

            // Skip to the next track, but never loop forever over a list of broken files.
            if (attempts < Math.Min(5, Current.Files.Count - 1))
            {
                var next = _queue.Next(true);
                if (next >= 0) StartCurrent(attempts + 1);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_ignoreStop || sender != _output) return;

        if (e.Exception is not null)
        {
            Logger.Warn("Playback error: " + e.Exception.Message);
            Problem?.Invoke("music_error_file");
        }

        // The track ended by itself: go on with the queue.
        var next = _queue.Next(true);
        if (next < 0)
        {
            ReleaseOutput();
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        StartCurrent();
    }

    private void ReleaseOutput()
    {
        _ignoreStop = true;
        try
        {
            if (_output is not null)
            {
                _output.PlaybackStopped -= OnPlaybackStopped;
                _output.Stop();
                _output.Dispose();
            }

            _reader?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warn("Releasing audio output failed: " + ex.Message);
        }
        finally
        {
            _output = null;
            _reader = null;
            _ignoreStop = false;
        }
    }

    // --------------------------------------------------------------- playlists

    public void SelectPlaylist(string id)
    {
        var playlist = Playlists.FirstOrDefault(p => p.Id == id);
        if (playlist is null || playlist == Current) return;

        ReleaseOutput();
        Current = playlist;
        _queue.Reset(Current.Files.Count, Shuffle);
        _updateSettings(s => s.LastPlaylistId = playlist.Id);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Playlist CreatePlaylist(string name)
    {
        var playlist = new Playlist { Name = string.IsNullOrWhiteSpace(name) ? "Playlist" : name.Trim() };
        Playlists.Add(playlist);
        SavePlaylists();
        SelectPlaylist(playlist.Id);
        return playlist;
    }

    public void RenamePlaylist(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        Current.Name = name.Trim();
        SavePlaylists();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteCurrentPlaylist()
    {
        if (Playlists.Count <= 1)
        {
            // Never leave the user without a playlist: just empty it.
            Current.Files.Clear();
        }
        else
        {
            ReleaseOutput();
            var index = Playlists.IndexOf(Current);
            Playlists.Remove(Current);
            Current = Playlists[Math.Min(index, Playlists.Count - 1)];
            _updateSettings(s => s.LastPlaylistId = Current.Id);
        }

        _queue.Reset(Current.Files.Count, Shuffle);
        SavePlaylists();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds files (unsupported ones and duplicates are skipped). Returns how many were added.</summary>
    public int AddFiles(IEnumerable<string> files)
    {
        var added = 0;
        foreach (var file in files)
        {
            if (!PlaylistStore.IsSupported(file)) continue;
            if (Current.Files.Contains(file, StringComparer.OrdinalIgnoreCase)) continue;
            Current.Files.Add(file);
            added++;
        }

        if (added > 0) AfterListChanged();
        return added;
    }

    public int AddFolder(string folder) => AddFiles(PlaylistStore.ScanFolder(folder));

    public void RemoveTrack(int index)
    {
        if (index < 0 || index >= Current.Files.Count) return;

        var playingRemoved = index == CurrentIndex;
        if (playingRemoved) ReleaseOutput();

        Current.Files.RemoveAt(index);
        AfterListChanged();
    }

    private void AfterListChanged()
    {
        var currentPath = CurrentPath;
        _queue.Reset(Current.Files.Count, Shuffle);
        if (_reader is not null && currentPath is not null)
        {
            var newIndex = Current.Files.FindIndex(f => string.Equals(f, currentPath, StringComparison.OrdinalIgnoreCase));
            if (newIndex >= 0) _queue.SelectIndex(newIndex);
        }

        SavePlaylists();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SavePlaylists()
    {
        try
        {
            _store.Save(Playlists);
        }
        catch (Exception ex)
        {
            Logger.Error("Could not save playlists.", ex);
        }
    }

    public void Dispose() => ReleaseOutput();
}
