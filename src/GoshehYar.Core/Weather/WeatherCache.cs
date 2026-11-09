using System.Text.Json;

namespace GoshehYar.Core.Weather;

/// <summary>Stores the last successful weather response as JSON. Never throws; a corrupt file reads as "no cache".</summary>
public static class WeatherCache
{
    public const string FileName = "weather-cache.json";

    public static bool Save(WeatherSnapshot snapshot, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot));
            File.Move(tmp, path, true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static WeatherSnapshot? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var snap = JsonSerializer.Deserialize<WeatherSnapshot>(File.ReadAllText(path));
            if (snap is null) return null;
            snap.Days ??= new List<WeatherDay>();
            return snap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>True when the cached data belongs to (about) the given place.</summary>
    public static bool IsSamePlace(WeatherSnapshot snapshot, double latitude, double longitude) =>
        Math.Abs(snapshot.Latitude - latitude) < 0.1 && Math.Abs(snapshot.Longitude - longitude) < 0.1;
}
