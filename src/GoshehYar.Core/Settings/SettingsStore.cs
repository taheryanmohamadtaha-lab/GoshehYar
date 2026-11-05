using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoshehYar.Core.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON. Saving is atomic (temp file + replace) and keeps a
/// .bak copy of the previous version. A corrupt file is renamed aside and defaults are used instead of crashing.
/// </summary>
public sealed class SettingsStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, "settings.json");
    }

    public string FilePath { get; }

    /// <summary>Set when the last <see cref="Load"/> had to fall back to defaults.</summary>
    public string? LastLoadError { get; private set; }

    public AppSettings Load()
    {
        LastLoadError = null;

        if (!File.Exists(FilePath))
        {
            return CreateDefaults();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            LastLoadError = "settings.json is damaged: " + ex.Message;
            MoveCorruptFileAside();
            return CreateDefaults();
        }
        catch (IOException ex)
        {
            LastLoadError = "settings.json could not be read: " + ex.Message;
            return CreateDefaults();
        }
        catch (UnauthorizedAccessException ex)
        {
            LastLoadError = "settings.json could not be read: " + ex.Message;
            return CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, json);

        if (File.Exists(FilePath))
        {
            File.Replace(temp, FilePath, FilePath + ".bak");
        }
        else
        {
            File.Move(temp, FilePath);
        }
    }

    private static AppSettings CreateDefaults()
    {
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }

    private void MoveCorruptFileAside()
    {
        try
        {
            var target = Path.Combine(
                Path.GetDirectoryName(FilePath)!,
                $"settings.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(FilePath, target);
        }
        catch (Exception)
        {
            // Best effort: if we cannot move it, the next Save will overwrite it.
        }
    }
}
