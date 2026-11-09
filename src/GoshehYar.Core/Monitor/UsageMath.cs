using System.Globalization;

namespace GoshehYar.Core.Monitor;

public sealed record DiskInfo(string Name, long TotalBytes, long FreeBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);
    public double UsedPercent => UsageMath.Percent(UsedBytes, TotalBytes);
}

public sealed record BatteryInfo(int Percent, bool Charging, bool PluggedIn, TimeSpan? Remaining);

/// <summary>Pure calculations of the system monitor (no system calls, so they can be tested).</summary>
public static class UsageMath
{
    public static double Percent(double part, double whole) =>
        whole <= 0 ? 0 : Math.Clamp(part / whole * 100.0, 0, 100);

    /// <summary>
    /// CPU load between two GetSystemTimes samples. On Windows the kernel time already includes the idle time.
    /// Returns 0 when nothing elapsed or a counter went backwards.
    /// </summary>
    public static double CpuPercent(ulong idle0, ulong kernel0, ulong user0, ulong idle1, ulong kernel1, ulong user1)
    {
        if (idle1 < idle0 || kernel1 < kernel0 || user1 < user0) return 0;
        var total = (kernel1 - kernel0) + (user1 - user0);
        if (total == 0) return 0;
        var idle = idle1 - idle0;
        if (idle > total) return 0;
        return Percent(total - idle, total);
    }

    /// <summary>Bytes per second between two counter readings; 0 for the first sample or after a counter reset.</summary>
    public static double RatePerSecond(ulong previous, ulong current, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || current < previous) return 0;
        return (current - previous) / elapsed.TotalSeconds;
    }
}

/// <summary>Human readable sizes. Always uses English digits and a dot; the view model converts digits.</summary>
public static class ByteFormat
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Size(double bytes)
    {
        if (bytes < 0 || double.IsNaN(bytes)) bytes = 0;
        var i = 0;
        while (bytes >= 1024 && i < Units.Length - 1)
        {
            bytes /= 1024;
            i++;
        }

        var text = i == 0 ? bytes.ToString("0", CultureInfo.InvariantCulture)
            : bytes >= 100 ? bytes.ToString("0", CultureInfo.InvariantCulture)
            : bytes >= 10 ? bytes.ToString("0.#", CultureInfo.InvariantCulture)
            : bytes.ToString("0.##", CultureInfo.InvariantCulture);
        return text + " " + Units[i];
    }

    public static string Rate(double bytesPerSecond) => Size(bytesPerSecond) + "/s";

    public static string Uptime(TimeSpan span, string dayUnit)
    {
        var d = (int)span.TotalDays;
        return d > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} {1} {2:00}:{3:00}", d, dayUnit, span.Hours, span.Minutes)
            : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", span.Hours, span.Minutes, span.Seconds);
    }
}
