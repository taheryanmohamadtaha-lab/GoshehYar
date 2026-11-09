using System.Globalization;
using System.Text.Json;

namespace GoshehYar.Core.Weather;

/// <summary>Parses Open-Meteo JSON (geocoding and forecast). Tolerates missing fields; returns empty/null instead of throwing.</summary>
public static class WeatherParser
{
    public static List<GeoPlace> ParseGeocoding(string? json)
    {
        var list = new List<GeoPlace>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array) return list;

            foreach (var r in results.EnumerateArray())
            {
                if (r.ValueKind != JsonValueKind.Object) continue;
                var lat = GetDouble(r, "latitude");
                var lon = GetDouble(r, "longitude");
                var name = GetString(r, "name");
                if (lat is null || lon is null || name.Length == 0) continue;
                list.Add(new GeoPlace
                {
                    Name = name,
                    Region = GetString(r, "admin1"),
                    Country = GetString(r, "country"),
                    Latitude = lat.Value,
                    Longitude = lon.Value
                });
            }
        }
        catch (JsonException)
        {
            list.Clear();
        }

        return list;
    }

    /// <summary>Returns null when the JSON has no usable "current" block.</summary>
    public static WeatherSnapshot? ParseForecast(string? json, DateTime fetchedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("current", out var cur) || cur.ValueKind != JsonValueKind.Object) return null;

            var snap = new WeatherSnapshot
            {
                FetchedAtUtc = fetchedAtUtc,
                Latitude = GetDouble(root, "latitude") ?? 0,
                Longitude = GetDouble(root, "longitude") ?? 0,
                TemperatureC = GetDouble(cur, "temperature_2m"),
                FeelsLikeC = GetDouble(cur, "apparent_temperature"),
                Humidity = GetDouble(cur, "relative_humidity_2m"),
                WindKmh = GetDouble(cur, "wind_speed_10m"),
                Code = GetInt(cur, "weather_code"),
                IsDay = (GetInt(cur, "is_day") ?? 1) != 0
            };
            if (snap.TemperatureC is null) return null;

            if (root.TryGetProperty("daily", out var daily) && daily.ValueKind == JsonValueKind.Object &&
                daily.TryGetProperty("time", out var times) && times.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var t in times.EnumerateArray())
                {
                    if (t.ValueKind == JsonValueKind.String &&
                        DateOnly.TryParseExact(t.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        snap.Days.Add(new WeatherDay
                        {
                            Date = date,
                            Code = GetIndexedInt(daily, "weather_code", index),
                            MaxC = GetIndexedDouble(daily, "temperature_2m_max", index),
                            MinC = GetIndexedDouble(daily, "temperature_2m_min", index)
                        });
                    }

                    index++;
                }
            }

            return snap;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static double? GetDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? ToDouble(v) : null;

    private static int? GetInt(JsonElement e, string name)
    {
        var d = GetDouble(e, name);
        return d is null ? null : (int)Math.Round(d.Value);
    }

    private static double? GetIndexedDouble(JsonElement daily, string name, int index)
    {
        if (!daily.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        if (index < 0 || index >= arr.GetArrayLength()) return null;
        return ToDouble(arr[index]);
    }

    private static int? GetIndexedInt(JsonElement daily, string name, int index)
    {
        var d = GetIndexedDouble(daily, name, index);
        return d is null ? null : (int)Math.Round(d.Value);
    }

    private static double? ToDouble(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) && !double.IsNaN(d) && !double.IsInfinity(d)) return d;
        return null;
    }
}
