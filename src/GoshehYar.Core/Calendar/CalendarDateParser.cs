using GoshehYar.Core.Dates;
using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Calendar;

/// <summary>Reads a typed date such as 1405/08/14 (Persian) or 2026-11-05 (Gregorian). Persian and Arabic digits are accepted.</summary>
public static class CalendarDateParser
{
    public static bool TryParse(string? text, CalendarKind kind, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = new System.Text.StringBuilder();
        foreach (var ch in text.Trim())
        {
            if (ch is >= '۰' and <= '۹') normalized.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩') normalized.Append((char)('0' + (ch - '٠')));
            else normalized.Append(ch);
        }

        var parts = normalized.ToString().Split(new[] { '/', '-', '.', ' ', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var y)) return false;
        if (!int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var m)) return false;
        if (!int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var d)) return false;

        try
        {
            if (kind == CalendarKind.Persian)
            {
                if (y < 1 || y > 9377 || m < 1 || m > 12) return false;
                if (d < 1 || d > DateNames.DaysInJalaliMonth(y, m)) return false;
                date = DateNames.FromJalali(y, m, d);
                return true;
            }

            if (y < 1 || y > 9999 || m < 1 || m > 12) return false;
            if (d < 1 || d > DateTime.DaysInMonth(y, m)) return false;
            date = new DateTime(y, m, d);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
