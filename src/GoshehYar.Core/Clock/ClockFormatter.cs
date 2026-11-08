using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Clock;

/// <summary>Formats a time of day as text (12/24 hour, with or without seconds). Digits are Western; convert with DigitFormatter.</summary>
public static class ClockFormatter
{
    /// <summary>"14:05:09" (24h) or "2:05:09" (12h). The AM/PM text comes from <see cref="Meridiem"/>.</summary>
    public static string FormatTime(DateTime time, bool use24Hour, bool showSeconds)
    {
        var hour = use24Hour ? time.Hour : To12Hour(time.Hour);
        var hourText = use24Hour ? hour.ToString("00") : hour.ToString();
        var text = $"{hourText}:{time.Minute:00}";
        return showSeconds ? $"{text}:{time.Second:00}" : text;
    }

    /// <summary>Empty in 24h mode; otherwise AM/PM ("ق.ظ" / "ب.ظ" in Persian).</summary>
    public static string Meridiem(DateTime time, bool use24Hour, AppLanguage language)
    {
        if (use24Hour) return "";
        var isMorning = time.Hour < 12;
        if (language == AppLanguage.Persian) return isMorning ? "ق.ظ" : "ب.ظ";
        return isMorning ? "AM" : "PM";
    }

    /// <summary>Complete text, e.g. "2:05:09 PM" / "۱۴:۰۵" (digits Western here).</summary>
    public static string Format(DateTime time, bool use24Hour, bool showSeconds, AppLanguage language)
    {
        var text = FormatTime(time, use24Hour, showSeconds);
        var meridiem = Meridiem(time, use24Hour, language);
        return meridiem.Length == 0 ? text : $"{text} {meridiem}";
    }

    public static int To12Hour(int hour24)
    {
        var h = hour24 % 12;
        return h == 0 ? 12 : h;
    }

    /// <summary>Angle in degrees (0 = 12 o'clock, clockwise) of the hour hand.</summary>
    public static double HourAngle(DateTime time) =>
        ((time.Hour % 12) + time.Minute / 60.0 + time.Second / 3600.0) * 30.0;

    public static double MinuteAngle(DateTime time) => (time.Minute + time.Second / 60.0) * 6.0;

    public static double SecondAngle(DateTime time) => time.Second * 6.0;

    /// <summary>Milliseconds until the next full second (1..1000), used to align the timer.</summary>
    public static int MillisecondsToNextSecond(DateTime time) => 1000 - time.Millisecond;
}
