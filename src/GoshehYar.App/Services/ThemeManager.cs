using System.Windows;
using System.Windows.Media;
using GoshehYar.Core.Settings;
using GoshehYar.Core.Themes;

namespace GoshehYar.App.Services;

/// <summary>Publishes the active theme as DynamicResource brushes (Brush.Background, Brush.Accent, ...).</summary>
public static class ThemeManager
{
    public static void Apply(AppSettings settings)
    {
        var app = Application.Current;
        if (app is null) return;

        var palette = ThemeCatalog.Get(settings.Theme);
        var accent = string.IsNullOrEmpty(settings.AccentColor) ? palette.Accent : settings.AccentColor;

        app.Resources["Brush.Background"] = MakeBrush(palette.Background);
        app.Resources["Brush.Surface"] = MakeBrush(palette.Surface);
        app.Resources["Brush.Foreground"] = MakeBrush(palette.Foreground);
        app.Resources["Brush.Muted"] = MakeBrush(palette.Muted);
        app.Resources["Brush.Border"] = MakeBrush(palette.Border);
        app.Resources["Brush.Accent"] = MakeBrush(accent);
    }

    public static SolidColorBrush MakeBrush(string hex)
    {
        Color color;
        try
        {
            color = ColorConverter.ConvertFromString(hex) is Color parsed ? parsed : Colors.Gray;
        }
        catch (System.FormatException)
        {
            color = Colors.Gray;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
