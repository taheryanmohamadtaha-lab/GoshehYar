using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        Items.Add(_settingsItem);
        Items.Add(_pinItem);
        Items.Add(_hideItem);
        Items.Add(_exitItem);

        Refresh();
    }

    public ObservableCollection<PanelItem> Items { get; } = new();

    public Orientation Orientation { get; private set; } = Orientation.Vertical;

    public double ButtonSize { get; private set; } = 40;

    public double IconFontSize { get; private set; } = 17;

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
        ItemMargin = new Thickness(s.ItemSpacing / 2.0);
        PanelFont = new FontFamily(s.FontFamily);

        _settingsItem.ToolTip = Loc.Get("panel_settings", s.Language);
        _pinItem.Glyph = s.Pinned ? "" : ""; // unpin / pin
        _pinItem.ToolTip = Loc.Get(s.Pinned ? "panel_unpin" : "panel_pin", s.Language);
        _hideItem.ToolTip = Loc.Get("panel_hide", s.Language);
        _exitItem.ToolTip = Loc.Get("panel_exit", s.Language);

        OnPropertyChanged(string.Empty);
    }
}
