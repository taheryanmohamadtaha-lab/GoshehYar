namespace GoshehYar.Core.Settings;

/// <summary>Screen edge the floating panel is attached to.</summary>
public enum DockEdge
{
    Left,
    Right,
    Top,
    Bottom
}

/// <summary>Position of the panel along its edge (Start/End give a "corner" placement).</summary>
public enum DockAlignment
{
    Start,
    Center,
    End
}

public enum AppLanguage
{
    Persian,
    English
}

public enum DigitStyle
{
    Persian,
    English
}

public enum PanelDensity
{
    Compact,
    Comfortable
}

public enum ThemeId
{
    DarkModern,
    LightModern,
    MidnightBlue,
    PurpleNeon,
    Emerald,
    Ocean,
    MinimalBlack,
    GlassTransparent
}

public enum ImageFormat { Png, Jpg }

public enum AudioFormat { Wav, Mp3 }

public enum RepeatMode { None, One, All }

public enum BackupFrequency { Daily, Weekly }
