using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Monitor;
using GoshehYar.Core.Layout;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.ViewModels;

/// <summary>One button on the floating panel.</summary>
public sealed class PanelItem : ViewModelBase
{
    private string _glyph;
    private string _toolTip = "";

    public PanelItem(string glyph, ICommand command)
    {
        _glyph = glyph;
        Command = command;
    }

    public ICommand Command { get; }

    private string _liveText = "";

    /// <summary>True for a button that shows live text (the system monitor) instead of an icon.</summary>
    public bool HasLive { get; init; }

    public string LiveText
    {
        get => _liveText;
        set
        {
            if (_liveText == value) return;
            _liveText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Segoe MDL2 Assets glyph.</summary>
    public string Glyph
    {
        get => _glyph;
        set
        {
            if (_glyph == value) return;
            _glyph = value;
            OnPropertyChanged();
        }
    }

    public string ToolTip
    {
        get => _toolTip;
        set
        {
            if (_toolTip == value) return;
            _toolTip = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>
/// Content of the floating panel. Phase 1 shows the built-in commands; modules (screenshot, notes, ...)
/// are added in front of them from phase 2 on through the module registry.
/// </summary>
public sealed class PanelViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly PanelItem _settingsItem;
    private readonly PanelItem _pinItem;
    private readonly PanelItem _hideItem;
    private readonly PanelItem _exitItem;

    public PanelViewModel(AppController controller)
    {
        _controller = controller;

        _settingsItem = new PanelItem("", new RelayCommand(_controller.OpenSettings));
        _pinItem = new PanelItem("", new RelayCommand(_controller.TogglePin));
        _hideItem = new PanelItem("", new RelayCommand(_controller.HidePanelToStrip));
        _exitItem = new PanelItem("", new RelayCommand(_controller.Exit));

        Refresh();
    }

    public ObservableCollection<PanelItem> Items { get; } = new();

    // ---- live CPU / RAM on the system monitor button; sampled once a second, only while the panel is open ----

    private readonly SystemMonitorService _monitor = new();
    private DispatcherTimer? _liveTimer;
    private bool _haveCpu;
    private ulong _idle, _kernel, _user;

    /// <summary>Called by the panel window: true while the panel is fully shown.</summary>
    public void SetLive(bool on)
    {
        if (!_moduleItems.ContainsKey("monitor")) on = false;

        if (on)
        {
            if (_liveTimer is not null && _liveTimer.IsEnabled) return;
            _haveCpu = false;
            SampleLive();
            _liveTimer ??= CreateLiveTimer();
            _liveTimer.Start();
        }
        else
        {
            _liveTimer?.Stop();
        }
    }

    private DispatcherTimer CreateLiveTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => SampleLive();
        return timer;
    }

    private void SampleLive()
    {
        if (!_moduleItems.TryGetValue("monitor", out var item)) return;

        try
        {
            var cpuText = "--";
            if (_monitor.TryReadCpuTimes(out var idle, out var kernel, out var user))
            {
                if (_haveCpu)
                {
                    cpuText = Math.Round(UsageMath.CpuPercent(_idle, _kernel, _user, idle, kernel, user)).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                }

                _idle = idle;
                _kernel = kernel;
                _user = user;
                _haveCpu = true;
            }

            var ramText = "--";
            if (_monitor.TryReadMemory(out var total, out var available))
            {
                var used = total - Math.Min(total, available);
                ramText = Math.Round(UsageMath.Percent(used, total)).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            }

            if (cpuText == "--" && item.LiveText.StartsWith("CPU ", StringComparison.Ordinal) && !item.LiveText.StartsWith("CPU --", StringComparison.Ordinal))
            {
                cpuText = item.LiveText.Split('\n')[0][4..]; // first sample: keep the previous value until the next tick
            }

            var text = "CPU " + cpuText + "\nRAM " + ramText;
            item.LiveText = GoshehYar.Core.Localization.DigitFormatter.Format(text, _controller.Settings.Current.Digits);
        }
        catch (Exception ex)
        {
            Logger.Warn("Panel monitor sample failed: " + ex.Message);
        }
    }

    private readonly System.Collections.Generic.Dictionary<string, PanelItem> _moduleItems = new();
    private string _itemsKey = "";

    /// <summary>Tools (in the saved order, only enabled ones) followed by the built-in buttons.</summary>
    private void RebuildItems(AppSettings s)
    {
        var modules = _controller.Modules.GetOrderedEnabled(s.ModuleOrder, s.ModulesEnabled);
        var key = string.Join(",", System.Linq.Enumerable.Select(modules, m => m.Id));

        foreach (var module in modules)
        {
            if (!_moduleItems.TryGetValue(module.Id, out var item))
            {
                var captured = module;
                item = new PanelItem(module.Glyph, new RelayCommand(() => _controller.OpenModule(captured)))
                {
                    HasLive = module.Id == "monitor",
                    LiveText = module.Id == "monitor" ? "CPU --\nRAM --" : ""
                };
                _moduleItems[module.Id] = item;
            }

            item.ToolTip = Loc.Get(module.TitleKey, s.Language);
        }

        if (key == _itemsKey && Items.Count > 0) return;
        _itemsKey = key;

        Items.Clear();
        foreach (var module in modules) Items.Add(_moduleItems[module.Id]);
        Items.Add(_settingsItem);
        Items.Add(_pinItem);
        Items.Add(_hideItem);
        Items.Add(_exitItem);
    }

    public Orientation Orientation { get; private set; } = Orientation.Vertical;

    public double ButtonSize { get; private set; } = 40;

    public double IconFontSize { get; private set; } = 17;

    /// <summary>Font size of the small live text on the system monitor button.</summary>
    public double LiveFontSize { get; private set; } = 10;

    public Thickness ItemMargin { get; private set; } = new Thickness(3);

    public FontFamily PanelFont { get; private set; } = new FontFamily(AppSettings.DefaultFont);

    /// <summary>Re-reads the settings (edge, sizes, language, pin state).</summary>
    public void Refresh()
    {
        var s = _controller.Settings.Current;

        Orientation = PanelGeometry.IsVertical(s.Edge) ? Orientation.Vertical : Orientation.Horizontal;

        var padding = s.Density == PanelDensity.Compact ? 10 : 18;
        ButtonSize = s.IconSize + padding;
        IconFontSize = s.IconSize * 0.78;
        LiveFontSize = Math.Max(8, ButtonSize * 0.2);
        ItemMargin = new Thickness(s.ItemSpacing / 2.0);
        PanelFont = new FontFamily(s.FontFamily);

        RebuildItems(s);

        _settingsItem.ToolTip = Loc.Get("panel_settings", s.Language);
        _pinItem.Glyph = s.Pinned ? "" : ""; // unpin / pin
        _pinItem.ToolTip = Loc.Get(s.Pinned ? "panel_unpin" : "panel_pin", s.Language);
        _hideItem.ToolTip = Loc.Get("panel_hide", s.Language);
        _exitItem.ToolTip = Loc.Get("panel_exit", s.Language);

        OnPropertyChanged(string.Empty);
    }
}
