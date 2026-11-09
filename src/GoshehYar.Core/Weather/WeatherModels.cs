namespace GoshehYar.Core.Weather;

/// <summary>One place returned by the geocoding search.</summary>
public sealed class GeoPlace
{
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public string Country { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>"Name, Region, Country" without empty parts.</summary>
    public string Label
    {
        get
        {
            var parts = new List<string>();
            foreach (var p in new[] { Name, Region, Country })
            {
                if (!string.IsNullOrWhiteSpace(p) && !parts.Contains(p)) parts.Add(p);
            }

            return string.Join(", ", parts);
        }
    }
}

/// <summary>One forecast day. Temperatures are always stored in Celsius.</summary>
public sealed class WeatherDay
{
    public DateOnly Date { get; set; }
    public int? Code { get; set; }
    public double? MaxC { get; set; }
    public double? MinC { get; set; }
}

/// <summary>Current conditions plus the daily forecast. Temperatures in Celsius, wind in km/h.</summary>
public sealed class WeatherSnapshot
{
    public DateTime FetchedAtUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? TemperatureC { get; set; }
    public double? FeelsLikeC { get; set; }
    public double? Humidity { get; set; }
    public double? WindKmh { get; set; }
    public int? Code { get; set; }
    public bool IsDay { get; set; } = true;
    public List<WeatherDay> Days { get; set; } = new();
}
