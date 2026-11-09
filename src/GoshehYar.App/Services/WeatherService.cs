using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GoshehYar.Core.Settings;
using GoshehYar.Core.Weather;

namespace GoshehYar.App.Services;

/// <summary>Talks to Open-Meteo (no key needed). Never throws: failures return null.</summary>
public static class WeatherService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GoshehYar/1.0");
        return client;
    }

    public static string CachePath => System.IO.Path.Combine(AppPaths.DataDirectory, WeatherCache.FileName);

    /// <summary>Null means a network or server failure; an empty list means nothing found.</summary>
    public static async Task<List<GeoPlace>?> SearchAsync(string query, AppLanguage language, CancellationToken token)
    {
        try
        {
            var url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(query.Trim()) +
                      "&count=8&language=" + (language == AppLanguage.Persian ? "fa" : "en") + "&format=json";
            var json = await Http.GetStringAsync(url, token).ConfigureAwait(true);
            return WeatherParser.ParseGeocoding(json);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException) Logger.Error("Weather geocoding failed", ex);
            return null;
        }
    }

    /// <summary>Gets the forecast and caches it. Null on any failure.</summary>
    public static async Task<WeatherSnapshot?> FetchAsync(double latitude, double longitude, CancellationToken token)
    {
        try
        {
            var inv = CultureInfo.InvariantCulture;
            var url = "https://api.open-meteo.com/v1/forecast?latitude=" + latitude.ToString("0.####", inv) +
                      "&longitude=" + longitude.ToString("0.####", inv) +
                      "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m,is_day" +
                      "&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=7";
            var json = await Http.GetStringAsync(url, token).ConfigureAwait(true);
            var snapshot = WeatherParser.ParseForecast(json, DateTime.UtcNow);
            if (snapshot is null) return null;
            snapshot.Latitude = latitude;
            snapshot.Longitude = longitude;
            WeatherCache.Save(snapshot, CachePath);
            return snapshot;
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException) Logger.Error("Weather fetch failed", ex);
            return null;
        }
    }
}
