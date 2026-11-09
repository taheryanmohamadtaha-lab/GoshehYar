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

    // ----- Clock & calendar (phase 3) -----
    public bool Clock24Hour { get; set; } = true;
    public bool ClockShowSeconds { get; set; } = true;
    public bool ClockShowAnalog { get; set; } = true;
    public CalendarKind CalendarKind { get; set; } = CalendarKind.Persian;

    /// <summary>Windows time-zone ids of the extra clocks (e.g. "Eastern Standard Time").</summary>
    public List<string> WorldClockZones { get; set; } = new() { "GMT Standard Time", "Eastern Standard Time", "Tokyo Standard Time" };

    // ----- Weather (phase 3). The place is chosen by the user by name; no GPS. -----
    public bool WeatherHasLocation { get; set; }
    public string WeatherCity { get; set; } = "";
    public string WeatherCountry { get; set; } = "";
    public double WeatherLatitude { get; set; }
    public double WeatherLongitude { get; set; }
    public bool WeatherFahrenheit { get; set; }

    // ----- Timer & Pomodoro (phase 3) -----
    public int PomodoroWorkMinutes { get; set; } = 25;
    public int PomodoroShortBreakMinutes { get; set; } = 5;
    public int PomodoroLongBreakMinutes { get; set; } = 15;
    public int PomodoroSessionsBeforeLongBreak { get; set; } = 4;
    public bool PomodoroAutoStartNext { get; set; } = true;
    public bool TimerSoundEnabled { get; set; } = true;

    /// <summary>Last countdown length in seconds.</summary>
    public int CountdownLastSeconds { get; set; } = 600;

    /// <summary>Local date (yyyy-MM-dd) the Pomodoro counter belongs to; the counter restarts on a new day.</summary>
    public string PomodoroCountDate { get; set; } = "";
    public int PomodoroCountToday { get; set; }

    // ----- Advanced appearance (phase 4) -----
    /// <summary>Corner radius of the panel in pixels (0 = square).</summary>
    public double PanelCornerRadius { get; set; } = 14;

    /// <summary>Length of the show/hide animation in milliseconds (0 = no animation).</summary>
    public int AnimationMs { get; set; } = 160;

    // ----- Per-tool hotkeys (phase 4): module id -> gesture text -----
    public Dictionary<string, string> ModuleHotkeys { get; set; } = new();

    // ----- Clipboard history (phase 4). Off by default: a clipboard can hold passwords. -----
    public bool ClipboardEnabled { get; set; }
    public int ClipboardMaxItems { get; set; } = 100;

    // ----- Modules (used from phase 2 on) -----
    public Dictionary<string, bool> ModulesEnabled { get; set; } = new();
    public List<string> ModuleOrder { get; set; } = new();

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.WorldClockZones = new List<string>(WorldClockZones ?? new List<string>());
        copy.ModulesEnabled = new Dictionary<string, bool>(ModulesEnabled ?? new Dictionary<string, bool>());
        copy.ModuleOrder = new List<string>(ModuleOrder ?? new List<string>());
        copy.ModuleHotkeys = new Dictionary<string, string>(ModuleHotkeys ?? new Dictionary<string, string>());
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

        if (!Enum.IsDefined(CalendarKind)) CalendarKind = CalendarKind.Persian;
        WorldClockZones ??= new List<string>();
        WorldClockZones = WorldClockZones.Where(z => !string.IsNullOrWhiteSpace(z)).Distinct().Take(8).ToList();
        WeatherCity ??= "";
        WeatherCountry ??= "";
        WeatherLatitude = Clamp(WeatherLatitude, -90, 90);
        WeatherLongitude = Clamp(WeatherLongitude, -180, 180);
        PomodoroWorkMinutes = (int)Clamp(PomodoroWorkMinutes, 1, 180);
        PomodoroShortBreakMinutes = (int)Clamp(PomodoroShortBreakMinutes, 1, 60);
        PomodoroLongBreakMinutes = (int)Clamp(PomodoroLongBreakMinutes, 1, 120);
        PomodoroSessionsBeforeLongBreak = (int)Clamp(PomodoroSessionsBeforeLongBreak, 2, 12);
        CountdownLastSeconds = (int)Clamp(CountdownLastSeconds, 1, 99 * 3600);
        PomodoroCountDate ??= "";
        PomodoroCountToday = (int)Clamp(PomodoroCountToday, 0, 999);

        ModulesEnabled ??= new Dictionary<string, bool>();
        ModuleOrder ??= new List<string>();

        PanelCornerRadius = Clamp(PanelCornerRadius, 0, 28);
        AnimationMs = (int)Clamp(AnimationMs, 0, 500);
        ClipboardMaxItems = (int)Clamp(ClipboardMaxItems, 10, 500);
        ModuleHotkeys ??= new Dictionary<string, string>();
        var cleaned = new Dictionary<string, string>();
        foreach (var pair in ModuleHotkeys)
        {
            // Keep only valid gestures, written in their canonical form.
            if (!string.IsNullOrWhiteSpace(pair.Key) && HotkeyGesture.TryParse(pair.Value, out var g) && g.HasModifier)
            {
                cleaned[pair.Key] = g.ToString();
            }
        }

        ModuleHotkeys = cleaned;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return min;
        return Math.Min(max, Math.Max(min, value));
    }
}
