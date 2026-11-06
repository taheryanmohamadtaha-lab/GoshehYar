using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GoshehYar.Core.Settings;
using NAudio.MediaFoundation;
using NAudio.Wave;

namespace GoshehYar.App.Services;

public enum RecorderState
{
    Idle,
    Recording,
    Paused
}

/// <summary>
/// Microphone recording to WAV (optionally converted to MP3). Recording starts only when the user asks for it
/// and the state is always visible. The service lives in the app, so recording continues if its window is closed.
/// </summary>
public sealed class AudioRecorderService : IDisposable
{
    private WaveInEvent? _input;
    private WaveFileWriter? _writer;
    private string _wavPath = "";
    private TaskCompletionSource<bool>? _stopped;
    private volatile float _level;

    public RecorderState State { get; private set; } = RecorderState.Idle;

    public event EventHandler? StateChanged;

    /// <summary>Peak level 0..1 of the latest audio block.</summary>
    public float Level => State == RecorderState.Recording ? _level : 0f;

    public TimeSpan Elapsed => _writer?.TotalTime ?? TimeSpan.Zero;

    public string? CurrentFile => State == RecorderState.Idle ? null : _wavPath;

    public static IReadOnlyList<string> GetMicrophones()
    {
        var list = new List<string>();
        try
        {
            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                list.Add(WaveInEvent.GetCapabilities(i).ProductName);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not list microphones: " + ex.Message);
        }

        return list;
    }

    /// <summary>Starts recording into <paramref name="wavPath"/>. Throws if the device cannot be opened.</summary>
    public void Start(int deviceIndex, string wavPath)
    {
        if (State != RecorderState.Idle) return;

        var folder = Path.GetDirectoryName(wavPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var input = new WaveInEvent
        {
            DeviceNumber = Math.Max(0, deviceIndex),
            WaveFormat = new WaveFormat(44100, 16, 1),
            BufferMilliseconds = 60
        };

        WaveFileWriter? writer = null;
        try
        {
            writer = new WaveFileWriter(wavPath, input.WaveFormat);
            input.DataAvailable += OnData;
            input.RecordingStopped += OnRecordingStopped;
            _wavPath = wavPath;
            _writer = writer;
            _input = input;
            _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            input.StartRecording();
        }
        catch
        {
            input.Dispose();
            writer?.Dispose();
            _writer = null;
            _input = null;
            try { if (File.Exists(wavPath)) File.Delete(wavPath); } catch (IOException) { }
            throw;
        }

        State = RecorderState.Recording;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (State != RecorderState.Recording) return;
        State = RecorderState.Paused;
        _level = 0;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        if (State != RecorderState.Paused) return;
        State = RecorderState.Recording;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops, finishes the file and converts it when MP3 is wanted. Returns the final file path.</summary>
    public async Task<StopResult> StopAsync(AudioFormat format)
    {
        if (State == RecorderState.Idle || _input is null) return new StopResult("", null);

        var wav = _wavPath;
        var stopped = _stopped!;
        _input.StopRecording();
        await stopped.Task.ConfigureAwait(true);

        State = RecorderState.Idle;
        _level = 0;
        StateChanged?.Invoke(this, EventArgs.Empty);

        if (format != AudioFormat.Mp3) return new StopResult(wav, null);

        var mp3 = Path.ChangeExtension(wav, ".mp3");
        try
        {
            await Task.Run(() =>
            {
                MediaFoundationApi.Startup();
                using var reader = new WaveFileReader(wav);
                MediaFoundationEncoder.EncodeToMp3(reader, mp3, 128000);
            }).ConfigureAwait(true);

            File.Delete(wav);
            return new StopResult(mp3, null);
        }
        catch (Exception ex)
        {
            Logger.Warn("MP3 conversion failed, WAV kept: " + ex.Message);
            try { if (File.Exists(mp3)) File.Delete(mp3); } catch (IOException) { }
            return new StopResult(wav, ex.Message);
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var writer = _writer;
        if (writer is null) return;

        float peak = 0;
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            var sample = Math.Abs((short)(e.Buffer[i] | (e.Buffer[i + 1] << 8))) / 32768f;
            if (sample > peak) peak = sample;
        }

        _level = peak;

        if (State == RecorderState.Recording)
        {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
            writer.Flush();
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _writer?.Dispose();
            _input?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error("Closing the recording failed.", ex);
        }
        finally
        {
            _writer = null;
            _input = null;
            _stopped?.TrySetResult(true);
        }
    }

    public void Dispose()
    {
        if (State == RecorderState.Idle) return;
        try
        {
            // Finish the WAV so the recording made so far is not lost.
            var input = _input;
            input?.StopRecording();
            _writer?.Dispose();
            _writer = null;
        }
        catch (Exception ex)
        {
            Logger.Error("Stopping the recorder on exit failed.", ex);
        }
    }
}

/// <summary>Final file and, if MP3 conversion failed, why the WAV was kept.</summary>
public sealed record StopResult(string Path, string? ConversionError);
