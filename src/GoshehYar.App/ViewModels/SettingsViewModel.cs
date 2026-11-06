using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GoshehYar.App.Services;
using GoshehYar.Core.Input;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;
using GoshehYar.Core.Themes;

namespace GoshehYar.App.ViewModels;

public sealed record Option<T>(T Value, string Label);

public sealed record AccentOption(string Hex, string Label, Brush Swatch);

/// <summary>One tool on/off switch in the settings window.</summary>
public sealed class ModuleToggle : ViewModelBase
{
    private readonly Func<bool> _get;
    private readonly Action<bool> _set;

    public ModuleToggle(string title, Func<bool> get, Action<bool> set)
    {
        Title = title;
        _get = get;
        _set = set;
    }

    public string Title { get; }

    public bool IsOn
    {
        get => _get();
        set { _set(value); OnPropertyChanged(); }
    }
}

/// <summary>Everything the settings window binds to. Changes apply live and are saved automatically.</summary>
public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private static readonly (string Hex, string Key)[] AccentPresets =
    {
        ("", "accent_default"),
        ("#4C9AFF", "accent_blue"),
        ("#2ED8A3", "accent_green"),
        ("#C04DFF", "accent_purple"),
        ("#FF6B6B", "accent_red"),
        ("#FFB020", "accent_amber"),
        ("#22B8E6", "accent_cyan"),
        ("#FF5FA2", "accent_pink"),
    };

    private readonly AppController _controller;
    private string? _hotkeyError;
    private bool _updating;

    public SettingsViewModel(AppController controller)
    {
        _controller = controller;

        FontNames = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ResetHotkeyCommand = new RelayCommand(ResetHotkey);
        ResetAllCommand = new RelayCommand(ResetAll);

        BuildOptions();
        _controller.Settings.Changed += OnSettingsChanged;
    }

    private AppSettings S => _controller.Settings.Current;

    // ------------------------------------------------------------------ text

    /// <summary>Localized text by key: <c>{Binding [lbl_theme]}</c>.</summary>
    public string this[string key] => Loc.Get(key, S.Language);

    public FlowDirection FlowDirection =>
        S.Language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string VersionText
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            var number = v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
            return DigitFormatter.Format("GoshehYar " + number, S.Digits);
        }
    }

    // --------------------------------------------------------------- options

    public IReadOnlyList<Option<AppLanguage>> Languages { get; private set; } = Array.Empty<Option<AppLanguage>>();
    public IReadOnlyList<Option<DigitStyle>> DigitStyles { get; private set; } = Array.Empty<Option<DigitStyle>>();
    public IReadOnlyList<Option<ThemeId>> Themes { get; private set; } = Array.Empty<Option<ThemeId>>();
    public IReadOnlyList<AccentOption> Accents { get; private set; } = Array.Empty<AccentOption>();
    public IReadOnlyList<Option<PanelDensity>> Densities { get; private set; } = Array.Empty<Option<PanelDensity>>();
    public IReadOnlyList<Option<DockEdge>> Edges { get; private set; } = Array.Empty<Option<DockEdge>>();
    public IReadOnlyList<Option<DockAlignment>> Alignments { get; private set; } = Array.Empty<Option<DockAlignment>>();
    public IReadOnlyList<Option<string>> Monitors { get; private set; } = Array.Empty<Option<string>>();
    public IReadOnlyList<string> FontNames { get; }
    public IReadOnlyList<ModuleToggle> ModuleToggles { get; private set; } = Array.Empty<ModuleToggle>();

    private void BuildOptions()
    {
        var lang = S.Language;
        string T(string key) => Loc.Get(key, lang);

        Languages = new[]
        {
            new Option<AppLanguage>(AppLanguage.Persian, T("lang_fa")),
            new Option<AppLanguage>(AppLanguage.English, T("lang_en")),
        };
        DigitStyles = new[]
        {
            new Option<DigitStyle>(DigitStyle.Persian, T("digits_fa")),
            new Option<DigitStyle>(DigitStyle.English, T("digits_en")),
        };
        Themes = ThemeCatalog.All
            .Select(id => new Option<ThemeId>(id, T("theme_" + id)))
            .ToList();
        Accents = AccentPresets
            .Select(p => new AccentOption(
                p.Hex,
                T(p.Key),
                p.Hex.Length == 0 ? Brushes.Transparent : ThemeManager.MakeBrush(p.Hex)))
            .ToList();
        Densities = new[]
        {
            new Option<PanelDensity>(PanelDensity.Compact, T("density_compact")),
            new Option<PanelDensity>(PanelDensity.Comfortable, T("density_comfortable")),
        };
        Edges = new[]
        {
            new Option<DockEdge>(DockEdge.Left, T("edge_left")),
            new Option<DockEdge>(DockEdge.Right, T("edge_right")),
            new Option<DockEdge>(DockEdge.Top, T("edge_top")),
            new Option<DockEdge>(DockEdge.Bottom, T("edge_bottom")),
        };
        Alignments = new[]
        {
            new Option<DockAlignment>(DockAlignment.Start, T("align_start")),
            new Option<DockAlignment>(DockAlignment.Center, T("align_center")),
            new Option<DockAlignment>(DockAlignment.End, T("align_end")),
        };

        ModuleToggles = _controller.Modules.All
            .OrderBy(m => m.DefaultOrder)
            .Select(m =>
            {
                var id = m.Id;
                return new ModuleToggle(
                    T(m.TitleKey),
                    () => !S.ModulesEnabled.TryGetValue(id, out var on) || on,
                    value => Apply(s => s.ModulesEnabled[id] = value));
            })
            .ToList();

        var monitors = new List<Option<string>> { new("", T("monitor_primary")) };
        var index = 1;
        foreach (var m in ScreenService.GetMonitors())
        {
            var size = $"{m.Bounds.Width:0}×{m.Bounds.Height:0}";
            var label = DigitFormatter.Format($"{index}: {size}", S.Digits) + (m.IsPrimary ? " ★" : "");
            monitors.Add(new Option<string>(m.DeviceName, label));
            index++;
        }

        // Keep a previously chosen monitor visible even if it is unplugged right now.
        if (!string.IsNullOrEmpty(S.MonitorDeviceName) && monitors.All(o => o.Value != S.MonitorDeviceName))
        {
            monitors.Add(new Option<string>(S.MonitorDeviceName, S.MonitorDeviceName));
        }

        Monitors = monitors;
    }

    // ------------------------------------------------------ settings: general

    public AppLanguage Language
    {
        get => S.Language;
        set
        {
            if (S.Language == value) return;
            Apply(s => s.Language = value);
            BuildOptions();
            OnPropertyChanged(string.Empty);
        }
    }

    public DigitStyle Digits
    {
        get => S.Digits;
        set
        {
            if (S.Digits == value) return;
            Apply(s => s.Digits = value);
            BuildOptions();
            OnPropertyChanged(string.Empty);
        }
    }

    public bool RunAtStartup
    {
        get => S.RunAtStartup;
        set => Apply(s => s.RunAtStartup = value, nameof(RunAtStartup));
    }

    // --------------------------------------------------- settings: appearance

    public ThemeId Theme
    {
        get => S.Theme;
        set => Apply(s => s.Theme = value, nameof(Theme));
    }

    public string Accent
    {
        get => S.AccentColor;
        set => Apply(s => s.AccentColor = value ?? "", nameof(Accent));
    }

    public string FontFamilyName
    {
        get => S.FontFamily;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            Apply(s => s.FontFamily = value, nameof(FontFamilyName));
        }
    }

    public double OpacityPercent
    {
        get => Math.Round(S.PanelOpacity * 100);
        set => Apply(s => s.PanelOpacity = value / 100.0, nameof(OpacityPercent), nameof(OpacityText));
    }

    public double IconSize
    {
        get => S.IconSize;
        set => Apply(s => s.IconSize = value, nameof(IconSize), nameof(IconSizeText));
    }

    public PanelDensity Density
    {
        get => S.Density;
        set => Apply(s => s.Density = value, nameof(Density));
    }

    public double ItemSpacing
    {
        get => S.ItemSpacing;
        set => Apply(s => s.ItemSpacing = value, nameof(ItemSpacing), nameof(ItemSpacingText));
    }

    // ------------------------------------------------------- settings: panel

    public DockEdge Edge
    {
        get => S.Edge;
        set => Apply(s => s.Edge = value, nameof(Edge));
    }

    public DockAlignment Alignment
    {
        get => S.Alignment;
        set => Apply(s => s.Alignment = value, nameof(Alignment));
    }

    public string Monitor
    {
        get => S.MonitorDeviceName;
        set => Apply(s => s.MonitorDeviceName = value ?? "", nameof(Monitor));
    }

    public double Thickness
    {
        get => S.PanelThickness;
        set => Apply(s => s.PanelThickness = value, nameof(Thickness), nameof(ThicknessText));
    }

    public double LengthPercent
    {
        get => S.PanelLengthPercent;
        set => Apply(s => s.PanelLengthPercent = value, nameof(LengthPercent), nameof(LengthText));
    }

    public double EdgeMargin
    {
        get => S.EdgeMargin;
        set => Apply(s => s.EdgeMargin = value, nameof(EdgeMargin), nameof(EdgeMarginText));
    }

    // --------------------------------------------------- settings: behaviour

    public bool AlwaysOnTop
    {
        get => S.AlwaysOnTop;
        set => Apply(s => s.AlwaysOnTop = value, nameof(AlwaysOnTop));
    }

    public bool AutoHide
    {
        get => S.AutoHide;
        set => Apply(s => s.AutoHide = value, nameof(AutoHide));
    }

    public double AutoHideDelayMs
    {
        get => S.AutoHideDelayMs;
        set => Apply(s => s.AutoHideDelayMs = (int)Math.Round(value), nameof(AutoHideDelayMs), nameof(AutoHideDelayText));
    }

    public double StripSize
    {
        get => S.HiddenStripSize;
        set => Apply(s => s.HiddenStripSize = value, nameof(StripSize), nameof(StripSizeText));
    }

    public bool Pinned
    {
        get => S.Pinned;
        set => Apply(s => s.Pinned = value, nameof(Pinned));
    }

    public bool PauseInFullscreen
    {
        get => S.PauseInFullscreen;
        set => Apply(s => s.PauseInFullscreen = value, nameof(PauseInFullscreen));
    }

    // ------------------------------------------------------ value read-outs

    public string OpacityText => Num(OpacityPercent);
    public string IconSizeText => Num(S.IconSize);
    public string ItemSpacingText => Num(S.ItemSpacing);
    public string ThicknessText => Num(S.PanelThickness);
    public string LengthText => Num(S.PanelLengthPercent);
    public string EdgeMarginText => Num(S.EdgeMargin);
    public string AutoHideDelayText => Num(S.AutoHideDelayMs);
    public string StripSizeText => Num(S.HiddenStripSize);

    private string Num(double value) =>
        DigitFormatter.Format(value.ToString("0", CultureInfo.InvariantCulture), S.Digits);

    // ---------------------------------------------------------------- hotkey

    public bool HotkeyEnabled
    {
        get => S.HotkeyEnabled;
        set
        {
            if (S.HotkeyEnabled == value) return;
            Apply(s => s.HotkeyEnabled = value, nameof(HotkeyEnabled));
            SyncHotkeyAndReport();
        }
    }

    public string HotkeyText => S.Hotkey;

    public string HotkeyMessage
    {
        get
        {
            if (_hotkeyError is not null) return _hotkeyError;
            return this[S.HotkeyEnabled ? "hotkey_hint" : "hotkey_disabled"];
        }
    }

    public ICommand ResetHotkeyCommand { get; }

    public ICommand ResetAllCommand { get; }

    /// <summary>Called by the window when the user presses a key combination in the hotkey box.</summary>
    public void CaptureHotkey(HotkeyModifiers modifiers, int virtualKey)
    {
        if (modifiers == HotkeyModifiers.None)
        {
            ShowHotkeyError("hotkey_need_modifier");
            return;
        }

        if (!HotkeyGesture.IsSupportedKey(virtualKey))
        {
            ShowHotkeyError("hotkey_unsupported");
            return;
        }

        TrySetHotkey(new HotkeyGesture(modifiers, virtualKey));
    }

    private void ResetHotkey() => TrySetHotkey(HotkeyGesture.Default);

    private void TrySetHotkey(HotkeyGesture gesture)
    {
        var previous = S.Hotkey;
        var wasEnabled = S.HotkeyEnabled;

        Apply(s =>
        {
            s.Hotkey = gesture.ToString();
            s.HotkeyEnabled = true;
        });

        var result = _controller.SyncHotkey();
        if (result == HotkeyResult.Registered)
        {
            _hotkeyError = null;
        }
        else
        {
            // Roll back to what worked before.
            Apply(s =>
            {
                s.Hotkey = previous;
                s.HotkeyEnabled = wasEnabled;
            });
            _controller.SyncHotkey();
            _hotkeyError = this["hotkey_conflict"];
        }

        OnPropertyChanged(nameof(HotkeyText));
        OnPropertyChanged(nameof(HotkeyEnabled));
        OnPropertyChanged(nameof(HotkeyMessage));
    }

    private void SyncHotkeyAndReport()
    {
        var result = _controller.SyncHotkey();
        _hotkeyError = result == HotkeyResult.Conflict ? this["hotkey_conflict"] : null;
        OnPropertyChanged(nameof(HotkeyMessage));
    }

    private void ShowHotkeyError(string key)
    {
        _hotkeyError = this[key];
        OnPropertyChanged(nameof(HotkeyMessage));
    }

    // ------------------------------------------------------------- reset all

    private void ResetAll()
    {
        var result = MessageBox.Show(
            this["confirm_reset_text"],
            this["confirm_reset_title"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            S.Language == AppLanguage.Persian
                ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                : MessageBoxOptions.None);

        if (result != MessageBoxResult.Yes) return;

        _updating = true;
        try
        {
            _controller.Settings.ResetToDefaults();
        }
        finally
        {
            _updating = false;
        }

        var hotkey = _controller.SyncHotkey();
        _hotkeyError = hotkey == HotkeyResult.Conflict ? this["hotkey_conflict"] : null;

        BuildOptions();
        OnPropertyChanged(string.Empty);
    }

    // ---------------------------------------------------------------- plumbing

    private void Apply(Action<AppSettings> change, params string[] changedProperties)
    {
        _updating = true;
        try
        {
            _controller.Settings.Update(change);
        }
        finally
        {
            _updating = false;
        }

        foreach (var name in changedProperties)
        {
            OnPropertyChanged(name);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Changes made elsewhere (e.g. the Pin button on the panel) refresh the window.
        if (_updating) return;
        OnPropertyChanged(string.Empty);
    }

    public void Dispose() => _controller.Settings.Changed -= OnSettingsChanged;
}
