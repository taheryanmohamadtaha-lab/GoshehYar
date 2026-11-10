using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GoshehYar.Core.Settings;
using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;

namespace GoshehYar.App.Services;

public enum RecorderState
{
    Idle,
    Recording,
    Paused
}

/// <summary>
/// Microphone or computer-sound (WASAPI loopback) recording to WAV (optionally converted to MP3), one source at a time. Recording starts only when the user asks for it
/// and the state is always visible. The service lives in the app, so recording continues if its window is closed.
/// </summary>
public sealed class AudioRecorderService : IDisposable
{
    private IWaveIn? _input;
    private WasapiOut? _silence;
    private bool _sourceIsFloat;
    private byte[] _convertBuffer = Array.Empty<byte>();
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
    public void Start(RecordingSource source, int deviceIndex, string wavPath)
    {
        if (State != RecorderState.Idle) return;

        var folder = Path.GetDirectoryName(wavPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        IWaveIn input;
        WaveFormat fileFormat;
        if (source == RecordingSource.SystemSound)
        {
            // Loopback of the default playback device. Delivers 32-bit float, which is converted to 16-bit below.
            var loopback = new WasapiLoopbackCapture();
            input = loopback;
            _sourceIsFloat = loopback.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat;
            fileFormat = _sourceIsFloat
                ? new WaveFormat(loopback.WaveFormat.SampleRate, 16, loopback.WaveFormat.Channels)
                : loopback.WaveFormat;
        }
        else
        {
            input = new WaveInEvent
            {
                DeviceNumber = Math.Max(0, deviceIndex),
                WaveFormat = new WaveFormat(44100, 16, 1),
                BufferMilliseconds = 60
            };
            _sourceIsFloat = false;
            fileFormat = input.WaveFormat;
        }

        WaveFileWriter? writer = null;
        try
        {
            writer = new WaveFileWriter(wavPath, fileFormat);
            input.DataAvailable += OnData;
            input.RecordingStopped += OnRecordingStopped;
            _wavPath = wavPath;
            _writer = writer;
            _input = input;
            _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (source == RecordingSource.SystemSound) StartSilenceKeeper(input.WaveFormat);
            input.StartRecording();
        }
        catch
        {
            StopSilenceKeeper();
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

    /// <summary>
    /// Loopback capture delivers nothing while the computer is silent, which would make the file shorter than the
    /// real time. Playing inaudible silence keeps the data flowing.
    /// </summary>
    private void StartSilenceKeeper(WaveFormat format)
    {
        try
        {
            var silence = new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels));
            _silence = new WasapiOut();
            _silence.Init(silence);
            _silence.Play();
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not start the silence keeper: " + ex.Message);
            StopSilenceKeeper();
        }
    }

    private void StopSilenceKeeper()
    {
        try
        {
            _silence?.Stop();
            _silence?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warn("Stopping the silence keeper failed: " + ex.Message);
        }
        finally
        {
            _silence = null;
        }
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

        var buffer = e.Buffer;
        var count = e.BytesRecorded;

        if (_sourceIsFloat)
        {
            // 32-bit float -> 16-bit PCM
            var samples = count / 4;
            var needed = samples * 2;
            if (_convertBuffer.Length < needed) _convertBuffer = new byte[needed];
            for (var i = 0; i < samples; i++)
            {
                var f = BitConverter.ToSingle(buffer, i * 4);
                if (float.IsNaN(f)) f = 0;
                var v = (short)Math.Round(Math.Clamp(f, -1f, 1f) * short.MaxValue);
                _convertBuffer[i * 2] = (byte)(v & 0xFF);
                _convertBuffer[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }

            buffer = _convertBuffer;
            count = needed;
        }

        float peak = 0;
        for (var i = 0; i + 1 < count; i += 2)
        {
            var sample = Math.Abs((short)(buffer[i] | (buffer[i + 1] << 8))) / 32768f;
            if (sample > peak) peak = sample;
        }

        _level = peak;

        if (State == RecorderState.Recording)
        {
            writer.Write(buffer, 0, count);
            writer.Flush();
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            StopSilenceKeeper();
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
            StopSilenceKeeper();
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
