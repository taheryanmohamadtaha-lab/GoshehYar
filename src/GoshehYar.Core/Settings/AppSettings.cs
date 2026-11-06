using System.Text.RegularExpressions;
using GoshehYar.Core.Input;

namespace GoshehYar.Core.Settings;

/// <summary>
/// All user settings. Persisted as JSON in %LOCALAPPDATA%\GoshehYar\settings.json.
/// Sizes are stored in device-independent units at 96 DPI and scaled per monitor at runtime.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultHotkey = "Ctrl+Alt+Space";
    public const string DefaultFont = "Segoe UI";

    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    // ----- Language & appearance -----
    public AppLanguage Language { get; set; } = AppLanguage.Persian;
    public DigitStyle Digits { get; set; } = DigitStyle.Persian;
    public ThemeId Theme { get; set; } = ThemeId.DarkModern;

    /// <summary>Optional accent override as #RRGGBB. Empty = use the theme's accent.</summary>
    public string AccentColor { get; set; } = "";

    public string FontFamily { get; set; } = DefaultFont;

    /// <summary>0.3 - 1.0</summary>
    public double PanelOpacity { get; set; } = 0.95;

    /// <summary>16 - 40</summary>
    public double IconSize { get; set; } = 22;

    public PanelDensity Density { get; set; } = PanelDensity.Comfortable;

    /// <summary>Gap between tools, 0 - 20.</summary>
    public double ItemSpacing { get; set; } = 6;

    // ----- Panel placement -----
    public DockEdge Edge { get; set; } = DockEdge.Right;
    public DockAlignment Alignment { get; set; } = DockAlignment.Center;

    /// <summary>Windows device name of the target monitor (e.g. \\.\DISPLAY2). Empty = primary monitor.</summary>
    public string MonitorDeviceName { get; set; } = "";

    /// <summary>Panel width (vertical) or height (horizontal), 40 - 120.</summary>
    public double PanelThickness { get; set; } = 56;

    /// <summary>Panel length as a percentage of the screen edge, 20 - 100.</summary>
    public double PanelLengthPercent { get; set; } = 40;

    /// <summary>Distance between the panel and the screen edge when shown, 0 - 40.</summary>
    public double EdgeMargin { get; set; } = 8;

    // ----- Behaviour -----
    public bool AlwaysOnTop { get; set; } = true;
    public bool AutoHide { get; set; } = true;

    /// <summary>Delay before auto-hide, 300 - 5000 ms.</summary>
    public int AutoHideDelayMs { get; set; } = 1200;

    /// <summary>Thickness of the thin handle that stays at the edge while hidden, 3 - 20.</summary>
    public double HiddenStripSize { get; set; } = 6;

    /// <summary>Pinned = never auto-hide.</summary>
    public bool Pinned { get; set; }

    /// <summary>Do not bother the user while a full-screen app / game / presentation is running.</summary>
    public bool PauseInFullscreen { get; set; } = true;

    public bool RunAtStartup { get; set; }

    // ----- Hotkey -----
    public bool HotkeyEnabled { get; set; } = true;
    public string Hotkey { get; set; } = DefaultHotkey;

    // ----- Screenshot -----
    /// <summary>Empty = Pictures\GoshehYar\Screenshots.</summary>
    public string ScreenshotFolder { get; set; } = "";
    public ImageFormat ScreenshotFormat { get; set; } = ImageFormat.Png;
    public int ScreenshotDelaySeconds { get; set; }
    public bool ScreenshotAutoSave { get; set; } = true;

    // ----- Audio recorder -----
    /// <summary>Empty = Music\GoshehYar\Recordings.</summary>
    public string RecordingsFolder { get; set; } = "";
    public AudioFormat RecordingFormat { get; set; } = AudioFormat.Wav;
    public string MicrophoneName { get; set; } = "";

    // ----- Music player -----
    public double MusicVolume { get; set; } = 0.7;
    public bool MusicShuffle { get; set; }
    public RepeatMode MusicRepeat { get; set; } = RepeatMode.All;
    public string LastPlaylistId { get; set; } = "";

    // ----- Backup -----
    public bool BackupAutoEnabled { get; set; } = true;
    public BackupFrequency BackupFrequency { get; set; } = BackupFrequency.Daily;
    /// <summary>Empty = %LOCALAPPDATA%\GoshehYar\backups.</summary>
    public string BackupFolder { get; set; } = "";
    public int BackupKeepCount { get; set; } = 7;
    public DateTime? LastBackupUtc { get; set; }

    // ----- Modules (used from phase 2 on) -----
    public Dictionary<string, bool> ModulesEnabled { get; set; } = new();
    public List<string> ModuleOrder { get; set; } = new();

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.ModulesEnabled = new Dictionary<string, bool>(ModulesEnabled ?? new Dictionary<string, bool>());
        copy.ModuleOrder = new List<string>(ModuleOrder ?? new List<string>());
        return copy;
    }

    /// <summary>Clamps every value into its valid range and repairs invalid data.</summary>
    public void Normalize()
    {
        SchemaVersion = CurrentSchemaVersion;

        if (!Enum.IsDefined(Language)) Language = AppLanguage.Persian;
        if (!Enum.IsDefined(Digits)) Digits = DigitStyle.Persian;
        if (!Enum.IsDefined(Theme)) Theme = ThemeId.DarkModern;
        if (!Enum.IsDefined(Density)) Density = PanelDensity.Comfortable;
        if (!Enum.IsDefined(Edge)) Edge = DockEdge.Right;
        if (!Enum.IsDefined(Alignment)) Alignment = DockAlignment.Center;

        AccentColor = AccentColor is not null && HexColor.IsMatch(AccentColor) ? AccentColor.ToUpperInvariant() : "";
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? DefaultFont : FontFamily.Trim();
        MonitorDeviceName ??= "";

        PanelOpacity = Clamp(PanelOpacity, 0.3, 1.0);
        IconSize = Clamp(IconSize, 16, 40);
        ItemSpacing = Clamp(ItemSpacing, 0, 20);
        PanelThickness = Clamp(PanelThickness, 40, 120);
        PanelLengthPercent = Clamp(PanelLengthPercent, 20, 100);
        EdgeMargin = Clamp(EdgeMargin, 0, 40);
        HiddenStripSize = Clamp(HiddenStripSize, 3, 20);
        AutoHideDelayMs = (int)Clamp(AutoHideDelayMs, 300, 5000);

        if (string.IsNullOrWhiteSpace(Hotkey) || !HotkeyGesture.TryParse(Hotkey, out var gesture))
        {
            Hotkey = DefaultHotkey;
        }
        else
        {
            Hotkey = gesture.ToString();
        }

        if (!Enum.IsDefined(ScreenshotFormat)) ScreenshotFormat = ImageFormat.Png;
        if (!Enum.IsDefined(RecordingFormat)) RecordingFormat = AudioFormat.Wav;
        if (!Enum.IsDefined(MusicRepeat)) MusicRepeat = RepeatMode.All;
        if (!Enum.IsDefined(BackupFrequency)) BackupFrequency = BackupFrequency.Daily;
        ScreenshotFolder ??= "";
        RecordingsFolder ??= "";
        MicrophoneName ??= "";
        LastPlaylistId ??= "";
        BackupFolder ??= "";
        ScreenshotDelaySeconds = (int)Clamp(ScreenshotDelaySeconds, 0, 30);
        MusicVolume = Clamp(MusicVolume, 0, 1);
        BackupKeepCount = (int)Clamp(BackupKeepCount, 1, 50);

        ModulesEnabled ??= new Dictionary<string, bool>();
        ModuleOrder ??= new List<string>();
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return min;
        return Math.Min(max, Math.Max(min, value));
    }
}
