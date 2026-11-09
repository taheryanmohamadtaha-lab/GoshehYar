namespace GoshehYar.Core.Localization;

/// <summary>Texts of one tool (Persian, English). Merged into <see cref="Loc"/>. Keys must be unique across all tables.</summary>
internal static class LocClock
{
    internal static readonly Dictionary<string, (string Fa, string En)> Table = new()
    {
        ["mod_clock"] = ("ساعت", "Clock"),
        ["clock_24h"] = ("نمایش ۲۴ ساعته", "24-hour format"),
        ["clock_seconds"] = ("نمایش ثانیه", "Show seconds"),
        ["clock_analog"] = ("نمایش ساعت عقربه‌ای", "Show analog clock"),
        ["clock_calendar"] = ("تقویم تاریخ", "Date calendar"),
        ["clock_cal_persian"] = ("شمسی (جلالی)", "Persian (Jalali)"),
        ["clock_cal_gregorian"] = ("میلادی", "Gregorian"),
        ["clock_world"] = ("ساعت شهرهای جهان", "World clocks"),
        ["clock_add_zone"] = ("افزودن شهر", "Add city"),
        ["clock_pick_zone"] = ("یک شهر یا منطقهٔ زمانی انتخاب کنید", "Choose a city or time zone"),
        ["clock_remove"] = ("حذف", "Remove"),
        ["clock_no_zones"] = ("هنوز شهری اضافه نشده است.", "No cities added yet."),
        ["clock_tomorrow"] = ("فردا", "Tomorrow"),
        ["clock_yesterday"] = ("دیروز", "Yesterday"),
        ["clock_today"] = ("امروز", "Today"),
        ["clock_diff_hours"] = ("{0} ساعت", "{0} h"),
        ["clock_same_time"] = ("هم‌زمان با شما", "Same as you"),
        ["clock_err_unknown"] = ("این منطقهٔ زمانی روی این رایانه پیدا نشد.", "This time zone was not found on this computer."),
        ["clock_err_max"] = ("حداکثر {0} شهر می‌توان اضافه کرد.", "You can add at most {0} cities."),
        ["clock_err_dup"] = ("این شهر قبلاً اضافه شده است.", "This city is already added."),
        ["clock_added"] = ("شهر اضافه شد.", "City added."),
        ["clock_local"] = ("ساعت محلی شما", "Your local time")
    };
}
