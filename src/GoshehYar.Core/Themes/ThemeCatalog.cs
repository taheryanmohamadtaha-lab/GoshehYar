using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Themes;

/// <summary>Colors of one theme as #RRGGBB or #AARRGGBB strings.</summary>
public sealed record ThemePalette(
    ThemeId Id,
    bool IsDark,
    string Background,
    string Surface,
    string Foreground,
    string Muted,
    string Accent,
    string Border);

public static class ThemeCatalog
{
    private static readonly Dictionary<ThemeId, ThemePalette> Palettes = new()
    {
        [ThemeId.DarkModern] = new(ThemeId.DarkModern, true, "#1E1F24", "#2A2C33", "#F3F4F6", "#9CA3AF", "#4C9AFF", "#3A3D46"),
        [ThemeId.LightModern] = new(ThemeId.LightModern, false, "#F7F8FA", "#E6E9EF", "#1F2328", "#6B7280", "#0B6BCB", "#D0D5DD"),
        [ThemeId.MidnightBlue] = new(ThemeId.MidnightBlue, true, "#0B1730", "#132447", "#E6EEFF", "#8FA3C7", "#5B8CFF", "#223765"),
        [ThemeId.PurpleNeon] = new(ThemeId.PurpleNeon, true, "#14081F", "#221036", "#F6E9FF", "#B79CD1", "#C04DFF", "#3B1D5C"),
        [ThemeId.Emerald] = new(ThemeId.Emerald, true, "#0C1F1A", "#14342B", "#E8FFF6", "#8DBFAE", "#2ED8A3", "#1F4D3F"),
        [ThemeId.Ocean] = new(ThemeId.Ocean, true, "#08202B", "#0E3345", "#E6F8FF", "#8CBBCC", "#22B8E6", "#1B4F66"),
        [ThemeId.MinimalBlack] = new(ThemeId.MinimalBlack, true, "#000000", "#141414", "#FFFFFF", "#8A8A8A", "#FFFFFF", "#2A2A2A"),
        [ThemeId.GlassTransparent] = new(ThemeId.GlassTransparent, true, "#A61E2230", "#33FFFFFF", "#FFFFFF", "#C5CAD6", "#7DB7FF", "#40FFFFFF"),
        // Light neon themes: pale background, vivid accent.
        [ThemeId.NeonPinkLight] = new(ThemeId.NeonPinkLight, false, "#FFF5FB", "#FFE0F1", "#2A0A1E", "#8A5472", "#F0127F", "#FFB3DA"),
        [ThemeId.NeonCyanLight] = new(ThemeId.NeonCyanLight, false, "#F2FEFF", "#D5F7FB", "#06222A", "#4B7F8A", "#00AFCF", "#9BE6F2"),
        [ThemeId.NeonLimeLight] = new(ThemeId.NeonLimeLight, false, "#F8FFEE", "#E2F9C8", "#14260A", "#5E7F45", "#46B800", "#BDE88C"),
        [ThemeId.NeonVioletLight] = new(ThemeId.NeonVioletLight, false, "#FAF3FF", "#EBD9FF", "#1F0A33", "#7B5E99", "#9D2CFF", "#D2B2FF"),
        [ThemeId.NeonOrangeLight] = new(ThemeId.NeonOrangeLight, false, "#FFF8F0", "#FFE6CC", "#2B1400", "#98693A", "#FF6A00", "#FFC999"),
    };

    public static IReadOnlyList<ThemeId> All { get; } = Enum.GetValues<ThemeId>();

    public static ThemePalette Get(ThemeId id) =>
        Palettes.TryGetValue(id, out var palette) ? palette : Palettes[ThemeId.DarkModern];
}
