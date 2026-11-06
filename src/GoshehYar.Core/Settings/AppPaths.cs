namespace GoshehYar.Core.Settings;

/// <summary>Well-known folders. All user data lives under %LOCALAPPDATA%\GoshehYar\.</summary>
public static class AppPaths
{
    public const string AppFolderName = "GoshehYar";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public static string NotesDatabasePath => Path.Combine(DataDirectory, "notes.db");

    public static string PlaylistsPath => Path.Combine(DataDirectory, "playlists.json");

    public static string DefaultBackupDirectory => Path.Combine(DataDirectory, "backups");

    /// <summary>Safety copies made right before a restore. Never pruned automatically.</summary>
    public static string RestoreSafetyDirectory => Path.Combine(DataDirectory, "restore-safety");

    public static string DefaultScreenshotsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), AppFolderName, "Screenshots");

    public static string DefaultRecordingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), AppFolderName, "Recordings");
}
