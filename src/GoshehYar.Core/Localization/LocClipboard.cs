namespace GoshehYar.Core.Localization;

/// <summary>Texts of the clipboard manager and shortcuts (Persian, English). Merged into <see cref="Loc"/>. Keys must be unique across all tables.</summary>
internal static class LocClipboard
{
    internal static readonly Dictionary<string, (string Fa, string En)> Table = new()
    {
        ["mod_clipboard"] = ("حافظه کلیپ‌بورد", "Clipboard history"),
        ["clip_enable"] = ("ثبت تاریخچه کلیپ‌بورد روشن باشد", "Record clipboard history"),
        ["clip_off_title"] = ("تاریخچه کلیپ‌بورد خاموش است", "Clipboard history is off"),
        ["clip_off_text"] = ("چیزی ثبت نمی‌شود. با روشن‌کردن آن، متن‌هایی که کپی می‌کنید روی همین رایانه ذخیره می‌شوند. متن رمزعبور مدیرهای رمز که خودشان را «غیرقابل ثبت» اعلام کنند ثبت نمی‌شود، اما بهتر است متن حساس را پین نکنید و گاهی تاریخچه را پاک کنید.", "Nothing is recorded. When turned on, text you copy is saved on this computer. Passwords from password managers that mark themselves private are skipped, but avoid pinning sensitive text and clear the history now and then."),
        ["clip_search"] = ("جست‌وجو در تاریخچه…", "Search history…"),
        ["clip_copy"] = ("کپی دوباره", "Copy again"),
        ["clip_pin"] = ("پین", "Pin"),
        ["clip_unpin"] = ("برداشتن پین", "Unpin"),
        ["clip_delete"] = ("حذف", "Delete"),
        ["clip_clear"] = ("پاک‌کردن تاریخچه (به‌جز پین‌شده‌ها)", "Clear history (keep pinned)"),
        ["clip_clear_all"] = ("پاک‌کردن همه", "Clear everything"),
        ["clip_confirm_clear_all"] = ("همه موارد از جمله پین‌شده‌ها حذف شوند؟", "Delete everything, including pinned items?"),
        ["clip_confirm_clear"] = ("تاریخچه (به‌جز پین‌شده‌ها) پاک شود؟", "Clear the history (pinned items stay)?"),
        ["clip_copied"] = ("کپی شد.", "Copied."),
        ["clip_cleared"] = ("پاک شد.", "Cleared."),
        ["clip_empty"] = ("هنوز چیزی ثبت نشده است. یک متن را کپی کنید.", "Nothing recorded yet. Copy some text."),
        ["clip_count"] = ("{0} مورد", "{0} items"),
        ["clip_chars"] = ("{0} نویسه", "{0} characters"),
        ["clip_db_error"] = ("پایگاه‌داده کلیپ‌بورد باز نشد.", "The clipboard database could not be opened."),
        ["clip_not_backed_up"] = ("تاریخچه کلیپ‌بورد در پشتیبان‌گیری گنجانده نمی‌شود.", "Clipboard history is not part of backups."),

        ["mod_shortcuts"] = ("میانبرهای ابزارها", "Tool shortcuts"),
        ["sc_hint"] = ("روی کادر هر ابزار کلیک کنید و کلیدهای دلخواه را بزنید (حتماً با Ctrl، Alt، Shift یا Win). با این میانبر آن ابزار مستقیم باز می‌شود.", "Click a tool's box and press the keys you want (with Ctrl, Alt, Shift or Win). The shortcut opens that tool directly."),
        ["sc_none"] = ("— بدون میانبر —", "— no shortcut —"),
        ["sc_clear"] = ("حذف", "Clear"),
        ["sc_conflict"] = ("این میانبر در سیستم یا برنامه دیگری استفاده می‌شود؛ ترکیب دیگری انتخاب کنید.", "This shortcut is used by Windows or another app; choose another combination."),
        ["sc_duplicate"] = ("این میانبر برای ابزار دیگری انتخاب شده است.", "This shortcut is already used by another tool."),
        ["sc_same_as_panel"] = ("این میانبر برای نمایش/مخفی‌کردن پنل است.", "This shortcut is the one that shows/hides the panel."),
        ["sc_need_modifier"] = ("حداقل یکی از Ctrl، Alt، Shift یا Win لازم است.", "Include at least one of Ctrl, Alt, Shift or Win."),
        ["sc_unsupported"] = ("این کلید پشتیبانی نمی‌شود.", "This key is not supported."),
        ["sc_saved"] = ("ذخیره شد.", "Saved."),
        ["sc_failed_at_start"] = ("میانبر {0} ثبت نشد (احتمالاً در جای دیگری استفاده شده است).", "Shortcut {0} could not be registered (probably used elsewhere)."),
    };
}
