namespace GoshehYar.Core.Clock;

/// <summary>Helpers for the world clocks: safe zone lookup, time in a zone, day offset and UTC offset text.</summary>
public static class WorldClock
{
    public const int MaxZones = 12;

    /// <summary>Finds a zone by its Windows id; returns null (never throws) when it does not exist on this computer.</summary>
    public static TimeZoneInfo? Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The time in <paramref name="zone"/> at the given UTC moment.</summary>
    public static DateTime TimeIn(TimeZoneInfo zone, DateTime utcNow)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
    }

    /// <summary>Calendar-day difference of the zone compared with the local zone: -1 yesterday, 0 same day, 1 tomorrow.</summary>
    public static int DayOffset(TimeZoneInfo zone, TimeZoneInfo local, DateTime utcNow)
    {
        var zoneDate = TimeIn(zone, utcNow).Date;
        var localDate = TimeIn(local, utcNow).Date;
        return (int)(zoneDate - localDate).TotalDays;
    }

    /// <summary>UTC offset of the zone at the given moment (daylight saving included).</summary>
    public static TimeSpan OffsetAt(TimeZoneInfo zone, DateTime utcNow)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return zone.GetUtcOffset(utc);
    }

    /// <summary>"UTC+3:30", "UTC-5", "UTC+0".</summary>
    public static string UtcOffsetText(TimeZoneInfo zone, DateTime utcNow) => "UTC" + SignedHours(OffsetAt(zone, utcNow));

    /// <summary>Hour difference of the zone from the local zone, e.g. "+5", "-3:30", "0".</summary>
    public static string DifferenceText(TimeZoneInfo zone, TimeZoneInfo local, DateTime utcNow)
    {
        var diff = OffsetAt(zone, utcNow) - OffsetAt(local, utcNow);
        return diff == TimeSpan.Zero ? "0" : SignedHours(diff);
    }

    /// <summary>City-like name: the Windows display name without its "(UTC+01:00)" prefix.</summary>
    public static string CityName(TimeZoneInfo zone)
    {
        var name = zone.DisplayName ?? "";
        if (name.StartsWith('('))
        {
            var close = name.IndexOf(')');
            if (close > 0 && close + 1 < name.Length) name = name[(close + 1)..];
        }

        name = name.Trim();
        return name.Length == 0 ? zone.Id : name;
    }

    /// <summary>Text for pickers: "Tehran (UTC+3:30)". Starts with the city so typing selects it.</summary>
    public static string PickerLabel(TimeZoneInfo zone, DateTime utcNow) => $"{CityName(zone)} ({UtcOffsetText(zone, utcNow)})";

    private static string SignedHours(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        var hours = (int)abs.TotalHours;
        var text = abs.Minutes == 0 ? hours.ToString() : $"{hours}:{abs.Minutes:00}";
        return sign + text;
    }
}
