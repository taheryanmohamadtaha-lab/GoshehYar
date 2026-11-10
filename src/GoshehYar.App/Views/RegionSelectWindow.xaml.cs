using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GoshehYar.App.Interop;
using GoshehYar.App.Services;
using GoshehYar.Core.Localization;
using GoshehYar.Core.Settings;

namespace GoshehYar.App.Views;

/// <summary>
/// Overlay for ONE monitor that shows a frozen copy of that monitor. The user drags a rectangle; the result is cut
/// out of that frozen copy, so nothing on screen can change between the click and the picture.
/// One overlay is opened per monitor (see <see cref="RegionSelector"/>), so every monitor keeps its own scale (DPI)
/// and mouse positions can never be mixed up between monitors.
/// </summary>
public partial class RegionSelectWindow : Window
{
    private readonly Bitmap _frozen;
    private readonly Rectangle _bounds;
    private System.Windows.Point _start;
    private bool _dragging;

    public RegionSelectWindow(Bitmap frozen, Rectangle bounds, AppLanguage language)
    {
        _frozen = frozen;
        _bounds = bounds;
        InitializeComponent();

        var source = ScreenshotService.ToBitmapSource(frozen);
        BaseImage.Source = source;
        BrightImage.Source = source;
        HintText.Text = Loc.Get("shot_region_hint", language);
        FlowDirection = language == AppLanguage.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        // Moving the window to another monitor changes its DPI; put it back on the exact monitor rectangle afterwards.
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(Place), DispatcherPriority.Background);
        Loaded += (_, _) => Place();
    }

    /// <summary>Raised once with the cropped picture, or with null when the user cancelled.</summary>
    public event Action<RegionSelectWindow, Bitmap?>? Completed;

    /// <summary>The monitor rectangle in physical pixels (virtual desktop coordinates).</summary>
    public Rectangle MonitorBounds => _bounds;

    private void Place()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(handle, new IntPtr(-1), _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _frozen.Dispose();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Place();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Place();
        Activate();
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Cancel();
    }

    private void OnCancel(object sender, MouseButtonEventArgs e) => Cancel();

    private void Cancel() => Completed?.Invoke(this, null);

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
            Completed?.Invoke(this, ScreenshotService.Crop(_frozen, rect));
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

/// <summary>Opens one selection overlay per monitor and waits until the user selects an area or cancels.</summary>
public static class RegionSelector
{
    /// <summary>Returns the selected picture, or null when cancelled. <paramref name="frozen"/> is the whole virtual desktop.</summary>
    public static Bitmap? Select(Bitmap frozen, AppLanguage language)
    {
        var virtualScreen = ScreenshotService.VirtualScreen;
        var windows = new List<RegionSelectWindow>();
        Bitmap? result = null;
        var finished = false;
        var frame = new DispatcherFrame();

        void Done(RegionSelectWindow sender, Bitmap? picture)
        {
            if (finished)
            {
                picture?.Dispose();
                return;
            }

            finished = true;
            result = picture;
            foreach (var w in windows.ToArray())
            {
                w.Completed -= Done;
                try { w.Close(); } catch (InvalidOperationException) { /* already closing */ }
            }

            frame.Continue = false;
        }

        foreach (var monitor in ScreenService.GetMonitors())
        {
            var bounds = new Rectangle((int)Math.Round(monitor.Bounds.X), (int)Math.Round(monitor.Bounds.Y),
                (int)Math.Round(monitor.Bounds.Width), (int)Math.Round(monitor.Bounds.Height));
            var relative = new Rectangle(bounds.X - virtualScreen.X, bounds.Y - virtualScreen.Y, bounds.Width, bounds.Height);

            Bitmap piece;
            try
            {
                piece = ScreenshotService.Crop(frozen, relative);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var window = new RegionSelectWindow(piece, bounds, language);
            window.Completed += Done;
            windows.Add(window);
        }

        if (windows.Count == 0) return null;

        foreach (var w in windows) w.Show();

        // Give the keyboard focus to the overlay under the mouse so Esc works at once.
        var cursor = System.Windows.Forms.Cursor.Position;
        var under = windows.Find(w => w.MonitorBounds.Contains(cursor.X, cursor.Y)) ?? windows[0];
        under.Activate();

        Dispatcher.PushFrame(frame);
        return result;
    }
}
