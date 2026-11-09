using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using GoshehYar.Core.Monitor;

namespace GoshehYar.App.Services;

/// <summary>Reads CPU, memory, disk, network and battery values. Read-only; nothing is stored.</summary>
public sealed class SystemMonitorService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
        public ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    public bool TryReadCpuTimes(out ulong idle, out ulong kernel, out ulong user)
    {
        idle = kernel = user = 0;
        try
        {
            if (!GetSystemTimes(out var i, out var k, out var u)) return false;
            idle = i.Value;
            kernel = k.Value;
            user = u.Value;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading CPU times failed: " + ex.Message);
            return false;
        }
    }

    public bool TryReadMemory(out ulong total, out ulong available)
    {
        total = available = 0;
        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status)) return false;
            total = status.TotalPhys;
            available = status.AvailPhys;
            return total > 0;
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading memory failed: " + ex.Message);
            return false;
        }
    }

    public IReadOnlyList<DiskInfo> ReadDisks()
    {
        var list = new List<DiskInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) continue;
                    if (!drive.IsReady) continue;
                    list.Add(new DiskInfo(drive.Name.TrimEnd('\\'), drive.TotalSize, drive.AvailableFreeSpace));
                }
                catch (IOException)
                {
                    // Drive vanished while reading; skip it.
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading disks failed: " + ex.Message);
        }

        return list;
    }

    /// <summary>Total bytes received and sent over all active non-loopback adapters since they came up.</summary>
    public bool TryReadNetworkTotals(out ulong received, out ulong sent)
    {
        received = sent = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var stats = nic.GetIPv4Statistics();
                received += (ulong)Math.Max(0, stats.BytesReceived);
                sent += (ulong)Math.Max(0, stats.BytesSent);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading network statistics failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>Null when the device has no battery.</summary>
    public BatteryInfo? ReadBattery()
    {
        try
        {
            var p = System.Windows.Forms.SystemInformation.PowerStatus;
            if (p.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)) return null;
            var percent = (int)Math.Round(Math.Clamp(p.BatteryLifePercent, 0f, 1f) * 100);
            var remaining = p.BatteryLifeRemaining > 0 ? TimeSpan.FromSeconds(p.BatteryLifeRemaining) : (TimeSpan?)null;
            return new BatteryInfo(
                percent,
                p.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.Charging),
                p.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online,
                remaining);
        }
        catch (Exception ex)
        {
            Logger.Warn("Reading battery failed: " + ex.Message);
            return null;
        }
    }
}
