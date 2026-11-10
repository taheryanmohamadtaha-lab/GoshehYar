namespace GoshehYar.Core.Clock;

/// <summary>Well-known cities with Persian and English names, each tied to the Windows time zone id it uses.</summary>
public static class WorldCities
{
    public sealed record City(string ZoneId, string Fa, string En);

    public static IReadOnlyList<City> Main { get; } = new City[]
    {
        new("Iran Standard Time", "تهران", "Tehran"),
        new("Turkey Standard Time", "استانبول", "Istanbul"),
        new("Arabian Standard Time", "دبی", "Dubai"),
        new("Arab Standard Time", "ریاض", "Riyadh"),
        new("Arabic Standard Time", "بغداد", "Baghdad"),
        new("Russian Standard Time", "مسکو", "Moscow"),
        new("GMT Standard Time", "لندن", "London"),
        new("W. Europe Standard Time", "برلین / پاریس / رم", "Berlin / Paris / Rome"),
        new("Egypt Standard Time", "قاهره", "Cairo"),
        new("Eastern Standard Time", "نیویورک", "New York"),
        new("Central Standard Time", "شیکاگو", "Chicago"),
        new("Mountain Standard Time", "دنور", "Denver"),
        new("Pacific Standard Time", "لس‌آنجلس", "Los Angeles"),
        new("E. South America Standard Time", "سائوپائولو", "São Paulo"),
        new("Pakistan Standard Time", "کراچی", "Karachi"),
        new("Afghanistan Standard Time", "کابل", "Kabul"),
        new("India Standard Time", "دهلی / بمبئی", "Delhi / Mumbai"),
        new("China Standard Time", "پکن", "Beijing"),
        new("Singapore Standard Time", "سنگاپور", "Singapore"),
        new("Korea Standard Time", "سئول", "Seoul"),
        new("Tokyo Standard Time", "توکیو", "Tokyo"),
        new("AUS Eastern Standard Time", "سیدنی", "Sydney"),
    };

    /// <summary>The city name for a zone id in the given language, or null when the zone is not one of the main cities.</summary>
    public static string? NameFor(string? zoneId, bool persian)
    {
        var city = Main.FirstOrDefault(c => string.Equals(c.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase));
        return city is null ? null : (persian ? city.Fa : city.En);
    }
}
