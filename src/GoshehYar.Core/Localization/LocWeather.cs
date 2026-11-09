namespace GoshehYar.Core.Localization;

/// <summary>Texts of one tool (Persian, English). Merged into <see cref="Loc"/>. Keys must be unique across all tables.</summary>
internal static class LocWeather
{
    internal static readonly Dictionary<string, (string Fa, string En)> Table = new()
    {
        ["mod_weather"] = ("آب‌وهوا", "Weather"),
        ["wx_city_hint"] = ("نام شهر را بنویسید…", "Type a city name…"),
        ["wx_search"] = ("جستجو", "Search"),
        ["wx_no_results"] = ("شهری پیدا نشد.", "No city found."),
        ["wx_search_failed"] = ("جستجو انجام نشد. اینترنت را بررسی کنید.", "Search failed. Check your internet connection."),
        ["wx_searching"] = ("در حال جستجو…", "Searching…"),
        ["wx_pick"] = ("یکی را انتخاب کنید:", "Pick one:"),
        ["wx_no_location"] = ("برای دیدن آب‌وهوا، ابتدا شهر خود را جستجو و انتخاب کنید.", "Search and pick your city to see the weather."),
        ["wx_change_city"] = ("تغییر شهر", "Change city"),
        ["wx_feels"] = ("احساس‌شده", "Feels like"),
        ["wx_humidity"] = ("رطوبت", "Humidity"),
        ["wx_wind"] = ("باد", "Wind"),
        ["wx_wind_unit"] = ("{0} کیلومتر بر ساعت", "{0} km/h"),
        ["wx_today"] = ("امروز", "Today"),
        ["wx_today_range"] = ("کمینه {0} / بیشینه {1}", "Low {0} / High {1}"),
        ["wx_next_days"] = ("روزهای آینده", "Next days"),
        ["wx_updated"] = ("آخرین به‌روزرسانی: {0}", "Last updated: {0}"),
        ["wx_offline"] = ("آفلاین - آخرین داده مربوط به {0}", "Offline - last data from {0}"),
        ["wx_fahrenheit"] = ("نمایش با فارنهایت", "Show in Fahrenheit"),
        ["wx_refresh"] = ("به‌روزرسانی", "Refresh"),
        ["wx_refreshing"] = ("در حال دریافت…", "Loading…"),
        ["wx_wait"] = ("لطفاً چند ثانیه صبر کنید.", "Please wait a few seconds."),
        ["wx_error"] = ("دریافت آب‌وهوا انجام نشد. بعداً دوباره تلاش کنید.", "Could not get the weather. Try again later."),
        ["wx_credit"] = ("Weather data by Open-Meteo.com", "Weather data by Open-Meteo.com"),
    };
}
