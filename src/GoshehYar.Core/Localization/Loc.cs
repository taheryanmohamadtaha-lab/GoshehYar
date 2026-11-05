using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Localization;

/// <summary>Tiny built-in string table (Persian + English). Keys use underscores so they work in XAML indexers.</summary>
public static class Loc
{
    private static readonly Dictionary<string, (string Fa, string En)> Table = new()
    {
        // application / tray
        ["app_name"] = ("گوشه‌یار", "GoshehYar"),
        ["tray_toggle"] = ("نمایش / مخفی کردن پنل", "Show / hide panel"),
        ["tray_hide_fully"] = ("مخفی کامل (فقط آیکون سینی)", "Hide completely (tray only)"),
        ["tray_settings"] = ("تنظیمات…", "Settings…"),
        ["tray_restart"] = ("راه‌اندازی مجدد", "Restart"),
        ["tray_exit"] = ("خروج", "Exit"),
        ["err_unexpected"] = ("خطای غیرمنتظره‌ای رخ داد؛ برنامه به کار خود ادامه می‌دهد.", "An unexpected error occurred; the app keeps running."),

        // panel buttons
        ["panel_settings"] = ("تنظیمات", "Settings"),
        ["panel_pin"] = ("سنجاق کردن پنل", "Pin panel"),
        ["panel_unpin"] = ("رها کردن پنل", "Unpin panel"),
        ["panel_hide"] = ("مخفی کردن پنل", "Hide panel"),
        ["panel_exit"] = ("خروج از برنامه", "Exit application"),

        // settings window
        ["settings_title"] = ("تنظیمات گوشه‌یار", "GoshehYar Settings"),
        ["sec_general"] = ("عمومی", "General"),
        ["sec_appearance"] = ("ظاهر", "Appearance"),
        ["sec_panel"] = ("پنل", "Panel"),
        ["sec_behavior"] = ("رفتار", "Behavior"),
        ["sec_hotkey"] = ("کلید میانبر", "Hotkey"),
        ["lbl_language"] = ("زبان برنامه", "Language"),
        ["lbl_digits"] = ("نوع اعداد", "Digits"),
        ["lbl_theme"] = ("تم", "Theme"),
        ["lbl_accent"] = ("رنگ اصلی", "Accent color"),
        ["lbl_font"] = ("فونت", "Font"),
        ["lbl_opacity"] = ("شفافیت پنل (٪)", "Panel opacity (%)"),
        ["lbl_icon_size"] = ("اندازه آیکون‌ها", "Icon size"),
        ["lbl_density"] = ("حالت نمایش", "Density"),
        ["lbl_spacing"] = ("فاصله بین ابزارها", "Spacing between tools"),
        ["lbl_edge"] = ("لبه صفحه", "Screen edge"),
        ["lbl_alignment"] = ("جای پنل روی لبه", "Position along the edge"),
        ["lbl_monitor"] = ("مانیتور", "Monitor"),
        ["lbl_thickness"] = ("ضخامت پنل", "Panel thickness"),
        ["lbl_length"] = ("طول پنل (٪ لبه)", "Panel length (% of edge)"),
        ["lbl_margin"] = ("فاصله از لبه", "Distance from edge"),
        ["lbl_always_on_top"] = ("همیشه روی سایر پنجره‌ها باشد", "Always on top"),
        ["lbl_auto_hide"] = ("مخفی شدن خودکار", "Auto-hide"),
        ["lbl_auto_hide_delay"] = ("زمان مخفی شدن (میلی‌ثانیه)", "Auto-hide delay (ms)"),
        ["lbl_strip_size"] = ("ضخامت نوار باریک در حالت مخفی", "Hidden handle thickness"),
        ["lbl_pinned"] = ("ثابت نگه داشتن پنل (بدون مخفی شدن)", "Keep panel pinned (never auto-hide)"),
        ["lbl_fullscreen"] = ("مزاحمت نشدن در برنامه‌های تمام‌صفحه", "Stay out of the way of full-screen apps"),
        ["lbl_startup"] = ("اجرای خودکار با شروع ویندوز", "Start with Windows"),
        ["lbl_hotkey_enabled"] = ("فعال بودن کلید میانبر", "Enable hotkey"),
        ["lbl_hotkey"] = ("نمایش / مخفی کردن پنل", "Show / hide panel"),
        ["btn_hotkey_reset"] = ("پیش‌فرض", "Default"),
        ["btn_reset_all"] = ("بازگردانی همه تنظیمات به حالت پیش‌فرض", "Reset all settings to defaults"),
        ["hotkey_hint"] = ("روی کادر کلیک کنید و ترکیب کلید دلخواه را بزنید.", "Click the box and press the key combination you want."),
        ["hotkey_ok"] = ("کلید میانبر فعال است.", "Hotkey is active."),
        ["hotkey_disabled"] = ("کلید میانبر غیرفعال است.", "Hotkey is disabled."),
        ["hotkey_conflict"] = ("این ترکیب توسط برنامه دیگری استفاده می‌شود. یک ترکیب دیگر انتخاب کنید.", "This combination is already used by another program. Pick a different one."),
        ["hotkey_need_modifier"] = ("حداقل یکی از کلیدهای Ctrl، Alt، Shift یا Win را همراه آن بگیرید.", "Hold at least one of Ctrl, Alt, Shift or Win together with the key."),
        ["hotkey_unsupported"] = ("این کلید پشتیبانی نمی‌شود.", "This key is not supported."),
        ["confirm_reset_title"] = ("بازگردانی تنظیمات", "Reset settings"),
        ["confirm_reset_text"] = ("همه تنظیمات به حالت پیش‌فرض برمی‌گردد (یادداشت‌ها و داده‌های شما پاک نمی‌شود). ادامه می‌دهید؟", "All settings return to their defaults (your notes and data are not touched). Continue?"),
        ["about_credit"] = ("سازنده: محمدطاها طاهریان", "Created by محمدطاها طاهریان"),

        // option labels
        ["edge_left"] = ("چپ", "Left"),
        ["edge_right"] = ("راست", "Right"),
        ["edge_top"] = ("بالا", "Top"),
        ["edge_bottom"] = ("پایین", "Bottom"),
        ["align_start"] = ("ابتدای لبه (گوشه)", "Start (corner)"),
        ["align_center"] = ("وسط", "Center"),
        ["align_end"] = ("انتهای لبه (گوشه)", "End (corner)"),
        ["lang_fa"] = ("فارسی", "Persian (فارسی)"),
        ["lang_en"] = ("English", "English"),
        ["digits_fa"] = ("فارسی (۱۲۳)", "Persian (۱۲۳)"),
        ["digits_en"] = ("انگلیسی (123)", "English (123)"),
        ["density_compact"] = ("فشرده", "Compact"),
        ["density_comfortable"] = ("گسترده", "Comfortable"),
        ["monitor_primary"] = ("مانیتور اصلی", "Primary monitor"),
        ["theme_DarkModern"] = ("تیره مدرن", "Dark Modern"),
        ["theme_LightModern"] = ("روشن مدرن", "Light Modern"),
        ["theme_MidnightBlue"] = ("آبی نیمه‌شب", "Midnight Blue"),
        ["theme_PurpleNeon"] = ("بنفش نئون", "Purple Neon"),
        ["theme_Emerald"] = ("زمردی", "Emerald"),
        ["theme_Ocean"] = ("اقیانوسی", "Ocean"),
        ["theme_MinimalBlack"] = ("مشکی مینیمال", "Minimal Black"),
        ["theme_GlassTransparent"] = ("شیشه‌ای شفاف", "Glass Transparent"),
        ["accent_default"] = ("پیش‌فرض تم", "Theme default"),
        ["accent_blue"] = ("آبی", "Blue"),
        ["accent_green"] = ("سبز", "Green"),
        ["accent_purple"] = ("بنفش", "Purple"),
        ["accent_red"] = ("قرمز", "Red"),
        ["accent_amber"] = ("کهربایی", "Amber"),
        ["accent_cyan"] = ("فیروزه‌ای", "Cyan"),
        ["accent_pink"] = ("صورتی", "Pink"),
    };

    /// <summary>All defined keys (used by tests to make sure every key has both languages).</summary>
    public static IEnumerable<string> Keys => Table.Keys;

    public static string Get(string key, AppLanguage language)
    {
        if (!Table.TryGetValue(key, out var entry)) return key;
        return language == AppLanguage.Persian ? entry.Fa : entry.En;
    }
}
