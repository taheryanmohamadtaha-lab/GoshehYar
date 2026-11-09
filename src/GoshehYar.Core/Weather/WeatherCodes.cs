using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Weather;

/// <summary>WMO weather interpretation codes as documented by Open-Meteo: text (Persian / English) and an emoji icon.</summary>
public static class WeatherCodes
{
    private static readonly Dictionary<int, (string Fa, string En, string Icon)> Table = new()
    {
        [0] = ("آسمان صاف", "Clear sky", "☀️"),
        [1] = ("عمدتاً صاف", "Mainly clear", "🌤️"),
        [2] = ("نیمه ابری", "Partly cloudy", "⛅"),
        [3] = ("ابری", "Overcast", "☁️"),
        [45] = ("مه", "Fog", "🌫️"),
        [48] = ("مه یخ‌زده", "Depositing rime fog", "🌫️"),
        [51] = ("نمنم باران ضعیف", "Light drizzle", "🌦️"),
        [53] = ("نمنم باران متوسط", "Moderate drizzle", "🌦️"),
        [55] = ("نمنم باران شدید", "Dense drizzle", "🌧️"),
        [56] = ("نمنم یخ‌زده ضعیف", "Light freezing drizzle", "🌧️"),
        [57] = ("نمنم یخ‌زده شدید", "Dense freezing drizzle", "🌧️"),
        [61] = ("باران ضعیف", "Slight rain", "🌦️"),
        [63] = ("باران متوسط", "Moderate rain", "🌧️"),
        [65] = ("باران شدید", "Heavy rain", "🌧️"),
        [66] = ("باران یخ‌زده ضعیف", "Light freezing rain", "🌧️"),
        [67] = ("باران یخ‌زده شدید", "Heavy freezing rain", "🌧️"),
        [71] = ("برف ضعیف", "Slight snowfall", "🌨️"),
        [73] = ("برف متوسط", "Moderate snowfall", "🌨️"),
        [75] = ("برف سنگین", "Heavy snowfall", "❄️"),
        [77] = ("دانه‌های برف", "Snow grains", "❄️"),
        [80] = ("رگبار ضعیف", "Slight rain showers", "🌦️"),
        [81] = ("رگبار متوسط", "Moderate rain showers", "🌧️"),
        [82] = ("رگبار شدید", "Violent rain showers", "🌧️"),
        [85] = ("بارش برف ضعیف", "Slight snow showers", "🌨️"),
        [86] = ("بارش برف سنگین", "Heavy snow showers", "🌨️"),
        [95] = ("رعدوبرق", "Thunderstorm", "⛈️"),
        [96] = ("رعدوبرق با تگرگ ضعیف", "Thunderstorm with slight hail", "⛈️"),
        [97] = ("رعدوبرق شدید", "Heavy thunderstorm", "⛈️"),
        [99] = ("رعدوبرق با تگرگ شدید", "Thunderstorm with heavy hail", "⛈️"),
    };

    public static IReadOnlyCollection<int> KnownCodes => Table.Keys;

    public static bool IsKnown(int? code) => code is not null && Table.ContainsKey(code.Value);

    public static string Text(int? code, AppLanguage language)
    {
        if (code is not null && Table.TryGetValue(code.Value, out var e)) return language == AppLanguage.Persian ? e.Fa : e.En;
        return language == AppLanguage.Persian ? "نامشخص" : "Unknown";
    }

    /// <summary>Emoji for the code; at night clear/mainly clear use a moon.</summary>
    public static string Icon(int? code, bool isDay = true)
    {
        if (code is not null && Table.TryGetValue(code.Value, out var e))
        {
            if (!isDay && code.Value == 0) return "🌙";
            if (!isDay && code.Value == 1) return "🌙";
            if (!isDay && code.Value == 2) return "☁️";
            return e.Icon;
        }

        return "🌡️";
    }
}
