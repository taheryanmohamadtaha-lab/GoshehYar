namespace GoshehYar.Core.Localization;

/// <summary>Texts of the system monitor (Persian, English). Merged into <see cref="Loc"/>. Keys must be unique across all tables.</summary>
internal static class LocMonitor
{
    internal static readonly Dictionary<string, (string Fa, string En)> Table = new()
    {
        ["mod_monitor"] = ("مانیتور سیستم", "System monitor"),
        ["mon_cpu"] = ("پردازنده (CPU)", "Processor (CPU)"),
        ["mon_ram"] = ("حافظه (RAM)", "Memory (RAM)"),
        ["mon_ram_text"] = ("{0} از {1} در حال استفاده", "{0} of {1} in use"),
        ["mon_disks"] = ("دیسک‌ها", "Disks"),
        ["mon_disk_text"] = ("{0} آزاد از {1}", "{0} free of {1}"),
        ["mon_network"] = ("شبکه", "Network"),
        ["mon_down"] = ("دانلود", "Download"),
        ["mon_up"] = ("آپلود", "Upload"),
        ["mon_battery"] = ("باتری", "Battery"),
        ["mon_battery_charging"] = ("{0}٪ — در حال شارژ", "{0}% — charging"),
        ["mon_battery_plugged"] = ("{0}٪ — به برق وصل است", "{0}% — plugged in"),
        ["mon_battery_discharging"] = ("{0}٪ — روی باتری", "{0}% — on battery"),
        ["mon_battery_remaining"] = (" (حدود {0} مانده)", " (about {0} left)"),
        ["mon_no_battery"] = ("باتری‌ای در این دستگاه نیست", "No battery in this device"),
        ["mon_uptime"] = ("زمان روشن‌بودن سیستم", "System uptime"),
        ["mon_day_unit"] = ("روز", "d"),
        ["mon_unavailable"] = ("در دسترس نیست", "Not available"),
        ["mon_hint"] = ("اطلاعات فقط وقتی این پنجره باز است هر ثانیه خوانده می‌شود و هیچ‌جا ذخیره یا ارسال نمی‌شود.", "Values are read every second only while this window is open; nothing is saved or sent."),
    };
}
