using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GoshehYar.App.Interop;
using GoshehYar.App.Services;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Views;

/// <summary>
/// Full-screen overlay that shows a frozen copy of the desktop. The user drags a rectangle; the result is cut
/// out of that frozen copy, so nothing on screen can change between the click and the picture.
/// </summary>
public partial class RegionSelectWindow : Window
{
    private readonly Bitmap _frozen;
    private System.Windows.Point _start;
    private bool _dragging;

    public RegionSelectWindow(Bitmap frozen, AppLanguage language)
    {
        _frozen = frozen;
        InitializeComponent();

        var source = ScreenshotService.ToBitmapSource(frozen);
        BaseImage.Source = source;
        BrightImage.Source = source;
        HintText.Text = Loc.Get("shot_region_hint", language);
        FlowDirection = language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    /// <summary>The selected part of the screen; null when cancelled.</summary>
    public Bitmap? Result { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Cover the whole virtual desktop exactly, in physical pixels.
        var area = ScreenshotService.VirtualScreen;
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(handle, new IntPtr(-1), area.X, area.Y, area.Width, area.Height, 0);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Activate();
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Cancel();
    }

    private void OnCancel(object sender, MouseButtonEventArgs e) => Cancel();

    private void Cancel()
    {
        Result = null;
        DialogResult = false;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Surface);
        _dragging = true;
        Hint.Visibility = Visibility.Collapsed;
        Surface.CaptureMouse();
        UpdateSelection(_start);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging) UpdateSelection(e.GetPosition(Surface));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Surface.ReleaseMouseCapture();

        var rect = ToPixels(GetRect(_start, e.GetPosition(Surface)));
        if (rect.Width < 4 || rect.Height < 4)
        {
            // A plain click: let the user try again.
            Outline.Visibility = Visibility.Collapsed;
            SizeBadge.Visibility = Visibility.Collapsed;
            BrightImage.Clip = null;
            Hint.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            Result = ScreenshotService.Crop(_frozen, rect);
            DialogResult = true;
        }
        catch (ArgumentException)
        {
            Cancel();
        }
    }

    private void UpdateSelection(System.Windows.Point current)
    {
        var r = GetRect(_start, current);

        Outline.Visibility = Visibility.Visible;
        Canvas.SetLeft(Outline, r.X);
        Canvas.SetTop(Outline, r.Y);
        Outline.Width = r.Width;
        Outline.Height = r.Height;

        BrightImage.Clip = new RectangleGeometry(r);

        var px = ToPixels(r);
        SizeText.Text = $"{px.Width} × {px.Height}";
        SizeBadge.Visibility = Visibility.Visible;
        Canvas.SetLeft(SizeBadge, r.X);
        Canvas.SetTop(SizeBadge, Math.Max(0, r.Y - 26));
    }

    private static Rect GetRect(System.Windows.Point a, System.Windows.Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Window units to pixels of the frozen picture (independent of the monitor DPI).</summary>
    private Rectangle ToPixels(Rect r)
    {
        var scaleX = _frozen.Width / Math.Max(1.0, Surface.ActualWidth);
        var scaleY = _frozen.Height / Math.Max(1.0, Surface.ActualHeight);
        return new Rectangle(
            (int)Math.Round(r.X * scaleX),
            (int)Math.Round(r.Y * scaleY),
            (int)Math.Round(r.Width * scaleX),
            (int)Math.Round(r.Height * scaleY));
    }
}
