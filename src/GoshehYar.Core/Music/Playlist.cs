using System.Text.Json;
using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Music;

public sealed class Playlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<string> Files { get; set; } = new();
}

/// <summary>Playlists saved as JSON next to the other user data. Music files themselves are never copied.</summary>
public sealed class PlaylistStore
{
    public static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma", ".ogg" };

    private readonly string _path;

    public PlaylistStore(string path) => _path = path;

    public List<Playlist> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var list = JsonSerializer.Deserialize<List<Playlist>>(File.ReadAllText(_path), SettingsStore.JsonOptions);
                if (list is { Count: > 0 })
                {
                    foreach (var p in list) p.Files ??= new List<string>();
                    return list;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
        }

        return new List<Playlist> { new() { Name = "Default" } };
    }

    public void Save(IEnumerable<Playlist> playlists)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(playlists.ToList(), SettingsStore.JsonOptions));
        File.Move(temp, _path, overwrite: true);
    }

    public static bool IsSupported(string file) =>
        SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    /// <summary>Music files in a folder (sub-folders included), sorted by path.</summary>
    public static List<string> ScanFolder(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(IsSupported)
                .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }
}
