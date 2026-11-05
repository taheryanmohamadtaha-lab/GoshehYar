using GoshehYar.Core.Settings;

namespace GoshehYar.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_match_the_specification()
    {
        var s = new AppSettings();
        s.Normalize();

        Assert.Equal("Ctrl+Alt+Space", s.Hotkey);
        Assert.True(s.HotkeyEnabled);
        Assert.True(s.AutoHide);
        Assert.True(s.AlwaysOnTop);
        Assert.False(s.Pinned);
        Assert.Equal(AppLanguage.Persian, s.Language);
        Assert.Equal(ThemeId.DarkModern, s.Theme);
    }

    [Fact]
    public void Normalize_clamps_out_of_range_values()
    {
        var s = new AppSettings
        {
            PanelOpacity = 7,
            IconSize = 2,
            ItemSpacing = -5,
            PanelThickness = 9999,
            PanelLengthPercent = 0,
            EdgeMargin = 500,
            HiddenStripSize = 0,
            AutoHideDelayMs = 1
        };

        s.Normalize();

        Assert.Equal(1.0, s.PanelOpacity);
        Assert.Equal(16, s.IconSize);
        Assert.Equal(0, s.ItemSpacing);
        Assert.Equal(120, s.PanelThickness);
        Assert.Equal(20, s.PanelLengthPercent);
        Assert.Equal(40, s.EdgeMargin);
        Assert.Equal(3, s.HiddenStripSize);
        Assert.Equal(300, s.AutoHideDelayMs);
    }

    [Fact]
    public void Normalize_repairs_invalid_data()
    {
        var s = new AppSettings
        {
            AccentColor = "red",
            Hotkey = "nonsense",
            FontFamily = "  ",
            Edge = (DockEdge)99,
            PanelOpacity = double.NaN
        };

        s.Normalize();

        Assert.Equal("", s.AccentColor);
        Assert.Equal(AppSettings.DefaultHotkey, s.Hotkey);
        Assert.Equal(AppSettings.DefaultFont, s.FontFamily);
        Assert.Equal(DockEdge.Right, s.Edge);
        Assert.Equal(0.3, s.PanelOpacity);
    }

    [Fact]
    public void Normalize_keeps_valid_accent_in_upper_case()
    {
        var s = new AppSettings { AccentColor = "#4c9aff" };
        s.Normalize();
        Assert.Equal("#4C9AFF", s.AccentColor);
    }

    [Fact]
    public void Clone_is_independent()
    {
        var a = new AppSettings();
        a.ModuleOrder.Add("notes");
        a.ModulesEnabled["notes"] = true;

        var b = a.Clone();
        b.ModuleOrder.Add("clock");
        b.ModulesEnabled["notes"] = false;
        b.Edge = DockEdge.Left;

        Assert.Single(a.ModuleOrder);
        Assert.True(a.ModulesEnabled["notes"]);
        Assert.Equal(DockEdge.Right, a.Edge);
    }
}

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "goshehyar-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Load_without_file_returns_defaults()
    {
        var store = new SettingsStore(_dir);
        var s = store.Load();
        Assert.Equal(AppSettings.DefaultHotkey, s.Hotkey);
        Assert.Null(store.LastLoadError);
    }

    [Fact]
    public void Save_then_load_round_trips_values()
    {
        var store = new SettingsStore(_dir);
        var s = store.Load();
        s.Edge = DockEdge.Bottom;
        s.Theme = ThemeId.Ocean;
        s.Language = AppLanguage.English;
        s.Hotkey = "Ctrl+Shift+F9";
        s.ModulesEnabled["notes"] = false;
        store.Save(s);

        var loaded = new SettingsStore(_dir).Load();
        Assert.Equal(DockEdge.Bottom, loaded.Edge);
        Assert.Equal(ThemeId.Ocean, loaded.Theme);
        Assert.Equal(AppLanguage.English, loaded.Language);
        Assert.Equal("Ctrl+Shift+F9", loaded.Hotkey);
        Assert.False(loaded.ModulesEnabled["notes"]);
    }

    [Fact]
    public void Second_save_keeps_a_backup_of_the_previous_version()
    {
        var store = new SettingsStore(_dir);
        var s = store.Load();
        store.Save(s);
        s.Edge = DockEdge.Top;
        store.Save(s);

        Assert.True(File.Exists(store.FilePath + ".bak"));
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_is_kept_aside()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ this is not json");

        var s = store.Load();

        Assert.Equal(AppSettings.DefaultHotkey, s.Hotkey);
        Assert.NotNull(store.LastLoadError);
        Assert.Single(Directory.GetFiles(_dir, "settings.corrupt-*.json"));
    }

    [Fact]
    public void Unknown_enum_text_is_treated_as_corrupt_not_a_crash()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ \"Edge\": \"Diagonal\" }");

        var s = store.Load();

        Assert.Equal(DockEdge.Right, s.Edge);
    }

    [Fact]
    public void Values_in_file_are_normalized_on_load()
    {
        var store = new SettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ \"PanelOpacity\": 50, \"Hotkey\": \"???\" }");

        var s = store.Load();

        Assert.Equal(1.0, s.PanelOpacity);
        Assert.Equal(AppSettings.DefaultHotkey, s.Hotkey);
    }
}
