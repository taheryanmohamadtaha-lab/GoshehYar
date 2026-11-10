using System.Text.RegularExpressions;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Modules;
using GoshehYar.Core.Settings;
using GoshehYar.Core.Themes;

namespace GoshehYar.Tests;

public class ModuleRegistryTests
{
    private sealed record FakeModule(string Id, int DefaultOrder) : IModule
    {
        public string TitleKey => "t_" + Id;
        public string Glyph => "";
    }

    private static ModuleRegistry Create()
    {
        var r = new ModuleRegistry();
        r.Register(new FakeModule("notes", 20));
        r.Register(new FakeModule("clock", 10));
        r.Register(new FakeModule("tasks", 30));
        return r;
    }

    [Fact]
    public void Default_order_is_used_when_nothing_is_saved()
    {
        var ids = Create().GetOrderedEnabled(new List<string>(), new Dictionary<string, bool>()).Select(m => m.Id);
        Assert.Equal(new[] { "clock", "notes", "tasks" }, ids);
    }

    [Fact]
    public void Saved_order_wins_and_unknown_ids_are_ignored()
    {
        var ids = Create()
            .GetOrderedEnabled(new List<string> { "tasks", "ghost", "clock" }, new Dictionary<string, bool>())
            .Select(m => m.Id);
        Assert.Equal(new[] { "tasks", "clock", "notes" }, ids);
    }

    [Fact]
    public void Disabled_modules_are_hidden()
    {
        var ids = Create()
            .GetOrderedEnabled(new List<string>(), new Dictionary<string, bool> { ["clock"] = false })
            .Select(m => m.Id);
        Assert.Equal(new[] { "notes", "tasks" }, ids);
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var r = Create();
        Assert.Throws<ArgumentException>(() => r.Register(new FakeModule("notes", 1)));
    }
}

public class LocalizationTests
{
    [Fact]
    public void Every_key_has_both_languages()
    {
        foreach (var key in Loc.Keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(Loc.Get(key, AppLanguage.Persian)), key + " (fa)");
            Assert.False(string.IsNullOrWhiteSpace(Loc.Get(key, AppLanguage.English)), key + " (en)");
        }
    }

    [Fact]
    public void Unknown_key_returns_the_key()
    {
        Assert.Equal("nope", Loc.Get("nope", AppLanguage.Persian));
    }

    [Fact]
    public void Every_theme_has_a_translated_name()
    {
        foreach (var id in ThemeCatalog.All)
        {
            Assert.Contains("theme_" + id, Loc.Keys);
        }
    }

    [Fact]
    public void Persian_digits_are_applied_only_when_requested()
    {
        Assert.Equal("۱۲:۳۰ — ۴۵٪", DigitFormatter.Format("12:30 — 45٪", DigitStyle.Persian));
        Assert.Equal("12:30", DigitFormatter.Format("12:30", DigitStyle.English));
        Assert.Equal("", DigitFormatter.Format("", DigitStyle.Persian));
    }
}

public class ThemeCatalogTests
{
    private static readonly Regex Color = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

    [Fact]
    public void At_least_the_eight_required_themes_exist()
    {
        Assert.True(ThemeCatalog.All.Count >= 8);
    }

    [Fact]
    public void Every_palette_uses_valid_colors()
    {
        foreach (var id in ThemeCatalog.All)
        {
            var p = ThemeCatalog.Get(id);
            Assert.Equal(id, p.Id);
            foreach (var c in new[] { p.Background, p.Surface, p.Foreground, p.Muted, p.Accent, p.Border })
            {
                Assert.Matches(Color, c);
            }
        }
    }
}
