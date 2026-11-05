using System;
using System.IO;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Services;

/// <summary>Minimal file logger (%LOCALAPPDATA%\GoshehYar\logs). Never throws. Logs no personal content.</summary>
public static class Logger
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogsDirectory);
                var path = Path.Combine(AppPaths.LogsDirectory, "goshehyar.log");

                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var old = path + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(path, old);
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                if (exception is not null) line += Environment.NewLine + exception;
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Logging must never crash the app.
        }
    }
}
