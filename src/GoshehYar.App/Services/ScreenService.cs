using System.Collections.Generic;
using GoshehYar.App.Interop;
using GoshehYar.Core.Layout;
using WinForms = System.Windows.Forms;

namespace GoshehYar.App.Services;

/// <summary>A monitor with its work area (taskbar excluded) in physical pixels and its DPI scale (1.0 = 96 DPI).</summary>
public sealed record MonitorInfo(string DeviceName, RectD Bounds, RectD WorkArea, double Scale, bool IsPrimary);

public static class ScreenService
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        foreach (var screen in WinForms.Screen.AllScreens)
        {
            var b = screen.Bounds;
            var w = screen.WorkingArea;
            list.Add(new MonitorInfo(
                screen.DeviceName,
                new RectD(b.X, b.Y, b.Width, b.Height),
                new RectD(w.X, w.Y, w.Width, w.Height),
                GetScale(b.X + b.Width / 2, b.Y + b.Height / 2),
                screen.Primary));
        }

        return list;
    }

    /// <summary>Finds the monitor by device name; falls back to the primary (or first) monitor.</summary>
    public static MonitorInfo Resolve(string? deviceName)
    {
        var monitors = GetMonitors();

        if (!string.IsNullOrEmpty(deviceName))
        {
            foreach (var m in monitors)
            {
                if (string.Equals(m.DeviceName, deviceName, System.StringComparison.OrdinalIgnoreCase)) return m;
            }
        }

        foreach (var m in monitors)
        {
            if (m.IsPrimary) return m;
        }

        if (monitors.Count > 0) return monitors[0];

        // Extremely unlikely: no monitor reported.
        var fallback = new RectD(0, 0, 1920, 1080);
        return new MonitorInfo("", fallback, fallback, 1.0, true);
    }

    /// <summary>The number Windows shows for the display ("\\.\DISPLAY3" gives 3). Falls back to the list position.</summary>
    public static int DisplayNumber(string? deviceName, int fallback)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            var match = System.Text.RegularExpressions.Regex.Match(deviceName, @"DISPLAY(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > 0) return n;
        }

        return fallback;
    }

    private static double GetScale(int x, int y)
    {
        try
        {
            var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = x, Y = y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (monitor != System.IntPtr.Zero &&
                NativeMethods.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 &&
                dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (System.DllNotFoundException)
        {
        }
        catch (System.EntryPointNotFoundException)
        {
        }

        return 1.0;
    }
}
