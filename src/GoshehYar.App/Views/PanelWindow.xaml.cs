using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GoshehYar.App.Interop;
using GoshehYar.App.Services;
using GoshehYar.App.ViewModels;
using GoshehYar.Core.Layout;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Views;

public enum PanelState
{
    /// <summary>Window not shown at all (reachable only through the tray icon or the hotkey).</summary>
    Hidden,

    /// <summary>Only the thin handle at the screen edge is visible.</summary>
    Strip,

    /// <summary>Panel fully visible.</summary>
    Revealed
}

/// <summary>
/// The floating panel. It is one borderless window glued to a screen edge. "Hiding" shrinks the window to a thin
/// strip instead of moving it off-screen, so it can never leak onto a neighbouring monitor.
/// </summary>
public partial class PanelWindow : Window
{
    public static readonly DependencyProperty RevealProperty = DependencyProperty.Register(
        nameof(Reveal),
        typeof(double),
        typeof(PanelWindow),
        new PropertyMetadata(0.0, (d, _) => ((PanelWindow)d).OnRevealChanged()));

    private readonly AppController _controller;
    private readonly PanelViewModel _viewModel;
    private readonly DispatcherTimer _hideTimer;
    private readonly IntPtr _handle;

    private PanelState _stateValue = PanelState.Hidden;

    /// <summary>Panel state. The live CPU/RAM text on the monitor button runs only while the panel is revealed.</summary>
    private PanelState _state
    {
        get => _stateValue;
        set
        {
            _stateValue = value;
            _viewModel?.SetLive(value == PanelState.Revealed);
        }
    }
    private bool _hideArmed;
    private bool _suppressedByFullscreen;

    public PanelWindow(AppController controller)
    {
        InitializeComponent();

        _controller = controller;
        _viewModel = new PanelViewModel(controller);
        DataContext = _viewModel;

        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (_state == PanelState.Revealed && CanAutoHide && !IsMouseOver)
            {
                ShowStrip();
            }
        };

        _handle = new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>0 = only the handle strip, 1 = fully shown. Animated.</summary>
    public double Reveal
    {
        get => (double)GetValue(RevealProperty);
        set => SetValue(RevealProperty, value);
    }

    public PanelState State => _state;

    private AppSettings Settings => _controller.Settings.Current;

    private bool CanAutoHide => Settings.AutoHide && !Settings.Pinned;

    // ---------------------------------------------------------------- public API

    public void ShowRevealed(bool armAutoHide = false)
    {
        EnsureVisible();
        _state = PanelState.Revealed;
        _hideTimer.Stop();
        _hideArmed = armAutoHide;
        AnimateTo(1);

        if (armAutoHide && CanAutoHide && !IsMouseOver)
        {
            StartHideTimer();
        }
    }

    public void ShowStrip()
    {
        EnsureVisible();
        _hideTimer.Stop();
        _hideArmed = false;
        _state = PanelState.Strip;
        AnimateTo(0);
    }

    public void HideCompletely()
    {
        _hideTimer.Stop();
        _hideArmed = false;
        _state = PanelState.Hidden;
        BeginAnimation(RevealProperty, null);
        Hide();
    }

    /// <summary>Hotkey / tray click behaviour: show when hidden or collapsed, collapse when shown.</summary>
    public void Toggle()
    {
        if (_state == PanelState.Revealed)
        {
            ShowStrip();
        }
        else
        {
            ShowRevealed();
        }
    }

    /// <summary>Hides the panel while a screenshot is taken; disposing the result shows it again.</summary>
    public IDisposable HideTemporarily()
    {
        var wasVisible = IsVisible;
        if (wasVisible) Hide();
        return new RestoreOnDispose(() =>
        {
            if (wasVisible && _state != PanelState.Hidden && !_suppressedByFullscreen)
            {
                Show();
                ApplyLayout();
                ApplyGeometry();
            }
        });
    }

    private sealed class RestoreOnDispose : IDisposable
    {
        private Action? _restore;

        public RestoreOnDispose(Action restore) => _restore = restore;

        public void Dispose()
        {
            _restore?.Invoke();
            _restore = null;
        }
    }

    /// <summary>Called when a full-screen app / game / presentation starts or ends.</summary>
    public void OnFullscreenChanged(bool fullscreen)
    {
        _suppressedByFullscreen = fullscreen;

        if (fullscreen)
        {
            if (IsVisible) Hide();
            return;
        }

        if (_state != PanelState.Hidden && !IsVisible)
        {
            Show();
            ApplyLayout();
            ApplyGeometry();
        }
    }

    /// <summary>Re-reads the settings and updates look, size and position.</summary>
    public void ApplySettings()
    {
        var s = Settings;
        Opacity = s.PanelOpacity;
        Card.CornerRadius = new CornerRadius(s.PanelCornerRadius);
        Topmost = s.AlwaysOnTop;
        _viewModel.Refresh();
        ApplyLayout();
        ApplyGeometry();

        if (!CanAutoHide) _hideTimer.Stop();
    }

    // ------------------------------------------------------------------- window

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Keep the panel out of Alt+Tab.
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_TOOLWINDOW);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ApplyLayout();
        ApplyGeometry();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        _hideTimer.Stop();
        _hideArmed = true;

        if (_state == PanelState.Strip && !_suppressedByFullscreen)
        {
            _state = PanelState.Revealed;
            AnimateTo(1);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_state == PanelState.Revealed && CanAutoHide && _hideArmed)
        {
            StartHideTimer();
        }
    }

    // ---------------------------------------------------------------- animation

    private void EnsureVisible()
    {
        if (IsVisible) return;

        SetRevealImmediately(0);
        Show();
        ApplyLayout();
        ApplyGeometry();
    }

    private void SetRevealImmediately(double value)
    {
        BeginAnimation(RevealProperty, null);
        Reveal = value;
    }

    private void AnimateTo(double target)
    {
        if (!SystemParameters.ClientAreaAnimation || !IsVisible || Settings.AnimationMs <= 0)
        {
            SetRevealImmediately(target);
            return;
        }

        var animation = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(Settings.AnimationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(RevealProperty, animation);
    }

    private void StartHideTimer()
    {
        _hideTimer.Interval = TimeSpan.FromMilliseconds(Settings.AutoHideDelayMs);
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void OnRevealChanged()
    {
        if (HandleBar is not null)
        {
            // The accent handle is only needed while (almost) collapsed.
            HandleBar.Opacity = Math.Min(1, Math.Max(0, 1 - Reveal * 3));
        }

        ApplyGeometry();
    }

    // ------------------------------------------------------------------- layout

    /// <summary>The configured length, but never shorter than what all buttons need.</summary>
    private double EffectiveLengthPercent(MonitorInfo monitor)
    {
        var s = Settings;
        var edgeLength = PanelGeometry.IsVertical(s.Edge) ? monitor.WorkArea.Height : monitor.WorkArea.Width;
        if (edgeLength <= 0) return s.PanelLengthPercent;

        var neededDip = _viewModel.Items.Count * (_viewModel.ButtonSize + s.ItemSpacing) + 14;
        var neededPercent = neededDip * monitor.Scale / edgeLength * 100.0;
        return Math.Min(100, Math.Max(s.PanelLengthPercent, neededPercent));
    }

    /// <summary>Moves/resizes the window for the current Reveal value (physical pixels).</summary>
    private void ApplyGeometry()
    {
        if (_handle == IntPtr.Zero) return;

        var s = Settings;
        var monitor = ScreenService.Resolve(s.MonitorDeviceName);
        var scale = monitor.Scale;

        var rect = PanelGeometry.Compute(
            monitor.WorkArea,
            s.Edge,
            s.Alignment,
            s.PanelThickness * scale,
            EffectiveLengthPercent(monitor),
            s.EdgeMargin * scale,
            s.HiddenStripSize * scale,
            Reveal);

        NativeMethods.SetWindowPos(
            _handle,
            IntPtr.Zero,
            (int)Math.Round(rect.X),
            (int)Math.Round(rect.Y),
            Math.Max(1, (int)Math.Round(rect.Width)),
            Math.Max(1, (int)Math.Round(rect.Height)),
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>Sizes the card (full panel size) and anchors it so it slides out of the screen edge.</summary>
    private void ApplyLayout()
    {
        var s = Settings;
        var monitor = ScreenService.Resolve(s.MonitorDeviceName);
        var scale = monitor.Scale;
        var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (dpi <= 0) dpi = 1;

        var thickness = s.PanelThickness * scale;
        var length = PanelGeometry.GetShownLength(monitor.WorkArea, s.Edge, EffectiveLengthPercent(monitor), s.EdgeMargin * scale);
        var strip = s.HiddenStripSize * scale;

        var vertical = PanelGeometry.IsVertical(s.Edge);
        Card.Width = (vertical ? thickness : length) / dpi;
        Card.Height = (vertical ? length : thickness) / dpi;

        // The card is anchored to the side opposite the screen edge, so its far side tracks the window
        // boundary while the window grows out of the edge.
        switch (s.Edge)
        {
            case DockEdge.Left:
                Card.HorizontalAlignment = HorizontalAlignment.Right;
                Card.VerticalAlignment = VerticalAlignment.Top;
                break;
            case DockEdge.Right:
                Card.HorizontalAlignment = HorizontalAlignment.Left;
                Card.VerticalAlignment = VerticalAlignment.Top;
                break;
            case DockEdge.Top:
                Card.HorizontalAlignment = HorizontalAlignment.Left;
                Card.VerticalAlignment = VerticalAlignment.Bottom;
                break;
            default:
                Card.HorizontalAlignment = HorizontalAlignment.Left;
                Card.VerticalAlignment = VerticalAlignment.Top;
                break;
        }

        // The handle sits on the screen-edge side of the window.
        if (vertical)
        {
            HandleBar.Width = strip / dpi;
            HandleBar.Height = double.NaN;
            HandleBar.VerticalAlignment = VerticalAlignment.Stretch;
            HandleBar.HorizontalAlignment = s.Edge == DockEdge.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }
        else
        {
            HandleBar.Height = strip / dpi;
            HandleBar.Width = double.NaN;
            HandleBar.HorizontalAlignment = HorizontalAlignment.Stretch;
            HandleBar.VerticalAlignment = s.Edge == DockEdge.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        }
    }
}
