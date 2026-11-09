using GoshehYar.Core.Settings;
using GoshehYar.Core.Weather;

namespace GoshehYar.Tests;

public class WeatherTests
{
    private const string ForecastJson = """
    {
      "latitude": 35.7, "longitude": 51.4, "utc_offset_seconds": 12600, "timezone": "Asia/Tehran",
      "current_units": {"temperature_2m": "°C"},
      "current": {"time": "2026-11-08T12:00", "interval": 900, "temperature_2m": 18.4, "relative_humidity_2m": 40,
                  "apparent_temperature": 17.1, "is_day": 1, "weather_code": 2, "wind_speed_10m": 11.5},
      "daily": {"time": ["2026-11-08","2026-11-09","2026-11-10"],
                "weather_code": [2, 61, 95],
                "temperature_2m_max": [20.1, 17.0, null],
                "temperature_2m_min": [9.0, 8.5, 7.0]}
    }
    """;

    private const string GeoJson = """
    {"results":[
      {"id":1,"name":"Tehran","latitude":35.69,"longitude":51.42,"country":"Iran","admin1":"Tehran"},
      {"id":2,"name":"Tehran","latitude":34.0,"longitude":50.0,"country":"Iran"},
      {"id":3,"latitude":1,"longitude":2}
    ],"generationtime_ms":0.5}
    """;

    [Fact]
    public void ParsesForecast()
    {
        var s = WeatherParser.ParseForecast(ForecastJson, new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc));
        Assert.NotNull(s);
        Assert.Equal(18.4, s!.TemperatureC);
        Assert.Equal(17.1, s.FeelsLikeC);
        Assert.Equal(40, s.Humidity);
        Assert.Equal(11.5, s.WindKmh);
        Assert.Equal(2, s.Code);
        Assert.True(s.IsDay);
        Assert.Equal(3, s.Days.Count);
        Assert.Equal(new DateOnly(2026, 11, 9), s.Days[1].Date);
        Assert.Equal(61, s.Days[1].Code);
        Assert.Null(s.Days[2].MaxC);
        Assert.Equal(7.0, s.Days[2].MinC);
    }

    [Fact]
    public void ForecastToleratesMissingAndBadInput()
    {
        Assert.Null(WeatherParser.ParseForecast(null, DateTime.UtcNow));
        Assert.Null(WeatherParser.ParseForecast("not json", DateTime.UtcNow));
        Assert.Null(WeatherParser.ParseForecast("{}", DateTime.UtcNow));
        var s = WeatherParser.ParseForecast("{\"current\":{\"temperature_2m\":5}}", DateTime.UtcNow);
        Assert.NotNull(s);
        Assert.Empty(s!.Days);
        Assert.Null(s.Humidity);
    }

    [Fact]
    public void ParsesGeocoding()
    {
        var list = WeatherParser.ParseGeocoding(GeoJson);
        Assert.Equal(2, list.Count);
        Assert.Equal("Tehran, Iran", list[1].Label);
        Assert.Equal("Tehran, Iran", list[0].Label);
        Assert.Empty(WeatherParser.ParseGeocoding("{\"generationtime_ms\":1}"));
        Assert.Empty(WeatherParser.ParseGeocoding("garbage"));
    }

    [Fact]
    public void AllDocumentedCodesHaveTexts()
    {
        var codes = new[] { 0, 1, 2, 3, 45, 48, 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, 97, 99 };
        foreach (var c in codes)
        {
            Assert.True(WeatherCodes.IsKnown(c), "code " + c);
            Assert.False(string.IsNullOrWhiteSpace(WeatherCodes.Text(c, AppLanguage.Persian)));
            Assert.False(string.IsNullOrWhiteSpace(WeatherCodes.Text(c, AppLanguage.English)));
            Assert.False(string.IsNullOrWhiteSpace(WeatherCodes.Icon(c)));
        }

        Assert.Equal("Unknown", WeatherCodes.Text(1234, AppLanguage.English));
    }

    [Fact]
    public void TemperatureFormatting()
    {
        Assert.Equal("0°", TemperatureUnits.Format(-0.2, false));
        Assert.Equal("32°", TemperatureUnits.Format(0, true));
        Assert.Equal("23°", TemperatureUnits.Format(22.6, false));
        Assert.Equal("--", TemperatureUnits.Format(null, false));
    }

    [Fact]
    public void CacheRoundTripAndCorruption()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gy-weather-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, WeatherCache.FileName);
        try
        {
            Assert.Null(WeatherCache.Load(path));
            var s = WeatherParser.ParseForecast(ForecastJson, new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc))!;
            Assert.True(WeatherCache.Save(s, path));
            var back = WeatherCache.Load(path);
            Assert.NotNull(back);
            Assert.Equal(18.4, back!.TemperatureC);
            Assert.Equal(3, back.Days.Count);
            Assert.Equal(new DateOnly(2026, 11, 10), back.Days[2].Date);
            Assert.True(WeatherCache.IsSamePlace(back, 35.69, 51.42));
            Assert.False(WeatherCache.IsSamePlace(back, 48.0, 2.0));

            File.WriteAllText(path, "{ broken");
            Assert.Null(WeatherCache.Load(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
