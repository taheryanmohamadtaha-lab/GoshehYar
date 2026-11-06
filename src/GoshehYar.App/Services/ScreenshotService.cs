using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using GoshehYar.App.Interop;
using GoshehYar.Core.Layout;
using GoshehYar.Core.Settings;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using CoreImageFormat = GoshehYar.Core.Settings.ImageFormat;

namespace GoshehYar.App.Services;

public sealed record WindowEntry(IntPtr Handle, string Title);

/// <summary>Screen capture helpers. All coordinates are physical pixels (the app is per-monitor DPI aware).</summary>
public static class ScreenshotService
{
    public static Rectangle VirtualScreen
    {
        get
        {
            var b = System.Windows.Forms.SystemInformation.VirtualScreen;
            return new Rectangle(b.X, b.Y, b.Width, b.Height);
        }
    }

    public static Bitmap CaptureRectangle(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0) throw new ArgumentException("Empty capture area.");

        var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    public static Bitmap CaptureVirtualScreen() => CaptureRectangle(VirtualScreen);

    public static Bitmap CaptureMonitor(MonitorInfo monitor) => CaptureRectangle(ToRectangle(monitor.Bounds));

    /// <summary>Cuts a part out of a bitmap (used by the region selector).</summary>
    public static Bitmap Crop(Bitmap source, Rectangle area)
    {
        area.Intersect(new Rectangle(0, 0, source.Width, source.Height));
        if (area.Width <= 0 || area.Height <= 0) throw new ArgumentException("Empty crop area.");
        return source.Clone(area, PixelFormat.Format32bppArgb);
    }

    private static Rectangle ToRectangle(RectD r) =>
        new((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));

    // ------------------------------------------------------------- windows

    public static List<WindowEntry> ListWindows()
    {
        var result = new List<WindowEntry>();
        var ownPid = (uint)Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                var length = NativeMethods.GetWindowTextLength(hWnd);
                if (length == 0) return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == ownPid) return true;

                var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;

                if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                {
                    return true;
                }

                var sb = new StringBuilder(length + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString().Trim();
                if (title.Length > 0 && title != "Program Manager") result.Add(new WindowEntry(hWnd, title));
            }
            catch (Exception)
            {
                // Skip windows we cannot inspect.
            }

            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>Brings the window to the front and captures its visible rectangle. Returns null if it is gone.</summary>
    public static Bitmap? CaptureWindow(IntPtr handle)
    {
        if (NativeMethods.IsIconic(handle)) NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(handle);
        System.Threading.Thread.Sleep(350);

        NativeMethods.RECT rect;
        if (NativeMethods.DwmGetWindowAttribute(handle, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out rect, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.RECT>()) != 0)
        {
            if (!NativeMethods.GetWindowRect(handle, out rect)) return null;
        }

        var area = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        area.Intersect(VirtualScreen);
        return area.Width > 0 && area.Height > 0 ? CaptureRectangle(area) : null;
    }

    // -------------------------------------------------------------- output

    public static string ResolveFolder(AppSettings s) =>
        string.IsNullOrWhiteSpace(s.ScreenshotFolder) ? AppPaths.DefaultScreenshotsDirectory : s.ScreenshotFolder;

    /// <summary>Saves as Screenshot_yyyy-MM-dd_HH-mm-ss.png/.jpg and returns the file path.</summary>
    public static string Save(Bitmap bitmap, string folder, CoreImageFormat format, DateTime when)
    {
        Directory.CreateDirectory(folder);
        var extension = format == CoreImageFormat.Jpg ? ".jpg" : ".png";
        var baseName = "Screenshot_" + when.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(folder, baseName + extension);
        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{baseName}_{i}{extension}");
        }

        if (format == CoreImageFormat.Jpg)
        {
            // JPG has no transparency: flatten on white first.
            using var flat = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(flat))
            {
                g.Clear(Color.White);
                g.DrawImageUnscaled(bitmap, 0, 0);
            }

            flat.Save(path, DrawingImageFormat.Jpeg);
        }
        else
        {
            bitmap.Save(path, DrawingImageFormat.Png);
        }

        return path;
    }

    /// <summary>WPF image for the preview and the clipboard.</summary>
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, DrawingImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static void CopyToClipboard(Bitmap bitmap) => System.Windows.Clipboard.SetImage(ToBitmapSource(bitmap));
}
