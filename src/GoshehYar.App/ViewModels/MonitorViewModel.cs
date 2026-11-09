using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using GoshehYar.App.Services;
using GoshehYar.Core.Monitor;

namespace GoshehYar.App.ViewModels;

public sealed class DiskRow : ViewModelBase
{
    private string _text = "";
    private double _percent;

    public DiskRow(string name) => Name = name;

    public string Name { get; }

    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; OnPropertyChanged(); } }
    }

    public double Percent
    {
        get => _percent;
        set { if (Math.Abs(_percent - value) > 0.01) { _percent = value; OnPropertyChanged(); } }
    }
}

/// <summary>Live CPU / RAM / disk / network / battery values, refreshed once a second while the window is open.</summary>
public sealed class MonitorViewModel : LocalizedViewModel
{
    private readonly SystemMonitorService _service = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private bool _haveCpu;
    private ulong _idle, _kernel, _user;
    private bool _haveNet;
    private ulong _rx, _tx;
    private TimeSpan _lastNetAt;
    private int _diskTick;

    private double _cpu, _ram;
    private string _cpuText = "", _ramText = "", _downText = "", _upText = "", _batteryText = "", _uptimeText = "";
    private double _batteryPercent;
    private bool _hasBattery;

    public MonitorViewModel(AppController controller) : base(controller)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        Tick();
        _timer.Start();
    }

    public ObservableCollection<DiskRow> Disks { get; } = new();

    public double Cpu { get => _cpu; private set { _cpu = value; OnPropertyChanged(); } }
    public string CpuText { get => _cpuText; private set { _cpuText = value; OnPropertyChanged(); } }
    public double Ram { get => _ram; private set { _ram = value; OnPropertyChanged(); } }
    public string RamText { get => _ramText; private set { _ramText = value; OnPropertyChanged(); } }
    public string DownText { get => _downText; private set { _downText = value; OnPropertyChanged(); } }
    public string UpText { get => _upText; private set { _upText = value; OnPropertyChanged(); } }
    public string BatteryText { get => _batteryText; private set { _batteryText = value; OnPropertyChanged(); } }
    public double BatteryPercent { get => _batteryPercent; private set { _batteryPercent = value; OnPropertyChanged(); } }
    public bool HasBattery { get => _hasBattery; private set { _hasBattery = value; OnPropertyChanged(); } }
    public string UptimeText { get => _uptimeText; private set { _uptimeText = value; OnPropertyChanged(); } }

    private void Tick()
    {
        try
        {
            ReadCpu();
            ReadRam();
            ReadNetwork();
            ReadBatteryAndUptime();
            if (_diskTick++ % 5 == 0) ReadDisks();
        }
        catch (Exception ex)
        {
            Logger.Error("System monitor refresh failed.", ex);
        }
    }

    private void ReadCpu()
    {
        if (!_service.TryReadCpuTimes(out var idle, out var kernel, out var user))
        {
            CpuText = this["mon_unavailable"];
            return;
        }

        if (_haveCpu)
        {
            Cpu = UsageMath.CpuPercent(_idle, _kernel, _user, idle, kernel, user);
            CpuText = Num(Math.Round(Cpu).ToString("0")) + (S.Language == Core.Settings.AppLanguage.Persian ? "٪" : "%");
        }

        _idle = idle;
        _kernel = kernel;
        _user = user;
        _haveCpu = true;
    }

    private void ReadRam()
    {
        if (!_service.TryReadMemory(out var total, out var available))
        {
            RamText = this["mon_unavailable"];
            return;
        }

        var used = total - Math.Min(total, available);
        Ram = UsageMath.Percent(used, total);
        RamText = Fmt("mon_ram_text", Num(ByteFormat.Size(used)), Num(ByteFormat.Size(total)));
    }

    private void ReadNetwork()
    {
        if (!_service.TryReadNetworkTotals(out var rx, out var tx))
        {
            DownText = UpText = this["mon_unavailable"];
            return;
        }

        var now = _clock.Elapsed;
        if (_haveNet)
        {
            var elapsed = now - _lastNetAt;
            DownText = Num(ByteFormat.Rate(UsageMath.RatePerSecond(_rx, rx, elapsed)));
            UpText = Num(ByteFormat.Rate(UsageMath.RatePerSecond(_tx, tx, elapsed)));
        }

        _rx = rx;
        _tx = tx;
        _lastNetAt = now;
        _haveNet = true;
    }

    private void ReadBatteryAndUptime()
    {
        var b = _service.ReadBattery();
        HasBattery = b is not null;
        if (b is null)
        {
            BatteryText = this["mon_no_battery"];
            BatteryPercent = 0;
        }
        else
        {
            BatteryPercent = b.Percent;
            var key = b.Charging ? "mon_battery_charging" : b.PluggedIn ? "mon_battery_plugged" : "mon_battery_discharging";
            var text = Fmt(key, b.Percent);
            if (b.Remaining is { } r && !b.PluggedIn)
            {
                text += Fmt("mon_battery_remaining", Num(string.Format("{0}:{1:00}", (int)r.TotalHours, r.Minutes)));
            }

            BatteryText = text;
        }

        UptimeText = Num(ByteFormat.Uptime(TimeSpan.FromMilliseconds(Environment.TickCount64), this["mon_day_unit"]));
    }

    private void ReadDisks()
    {
        var disks = _service.ReadDisks();
        // Update in place so the list does not flicker.
        for (var i = Disks.Count - 1; i >= 0; i--)
        {
            if (!disks.Any(d => d.Name == Disks[i].Name)) Disks.RemoveAt(i);
        }

        foreach (var d in disks)
        {
            var row = Disks.FirstOrDefault(r => r.Name == d.Name);
            if (row is null)
            {
                row = new DiskRow(d.Name);
                Disks.Add(row);
            }

            row.Percent = d.UsedPercent;
            row.Text = Fmt("mon_disk_text", Num(ByteFormat.Size(d.FreeBytes)), Num(ByteFormat.Size(d.TotalBytes)));
        }
    }

    protected override void OnSettingsChanged(object? sender, EventArgs e)
    {
        Tick();
        _diskTick = 0;
        base.OnSettingsChanged(sender, e);
    }

    public override void Dispose()
    {
        _timer.Stop();
        base.Dispose();
    }
}
