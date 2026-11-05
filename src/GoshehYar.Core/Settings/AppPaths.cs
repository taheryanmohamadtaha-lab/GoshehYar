namespace GoshehYar.Core.Settings;

/// <summary>Well-known folders. All user data lives under %LOCALAPPDATA%\GoshehYar\.</summary>
public static class AppPaths
{
    public const string AppFolderName = "GoshehYar";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
