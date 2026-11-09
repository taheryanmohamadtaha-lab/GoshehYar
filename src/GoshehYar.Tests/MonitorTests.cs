using GoshehYar.Core.Monitor;
using Xunit;

namespace GoshehYar.Tests;

public class MonitorTests
{
    [Fact]
    public void CpuPercentHalfBusy()
    {
        // kernel includes idle: total 200, idle 100 -> 50 % busy
        Assert.Equal(50, UsageMath.CpuPercent(0, 0, 0, 100, 150, 50), 3);
    }

    [Fact]
    public void CpuPercentGuardsBadInput()
    {
        Assert.Equal(0, UsageMath.CpuPercent(10, 10, 10, 10, 10, 10));
        Assert.Equal(0, UsageMath.CpuPercent(100, 100, 100, 50, 200, 200));
        Assert.Equal(0, UsageMath.CpuPercent(0, 0, 0, 500, 100, 100));
    }

    [Fact]
    public void RateHandlesResetAndZeroTime()
    {
        Assert.Equal(500, UsageMath.RatePerSecond(1000, 2000, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, UsageMath.RatePerSecond(2000, 1000, TimeSpan.FromSeconds(1)));
        Assert.Equal(0, UsageMath.RatePerSecond(0, 1000, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(5368709120, "5 GB")]
    public void SizeFormats(double bytes, string expected) => Assert.Equal(expected, ByteFormat.Size(bytes));

    [Fact]
    public void PercentClamps()
    {
        Assert.Equal(100, UsageMath.Percent(200, 100));
        Assert.Equal(0, UsageMath.Percent(5, 0));
    }

    [Fact]
    public void DiskUsedPercent()
    {
        var d = new DiskInfo("C:", 1000, 250);
        Assert.Equal(75, d.UsedPercent, 3);
    }

    [Fact]
    public void UptimeFormat()
    {
        Assert.Equal("01:02:03", ByteFormat.Uptime(new TimeSpan(1, 2, 3), "d"));
        Assert.Equal("2 d 03:04", ByteFormat.Uptime(new TimeSpan(2, 3, 4, 5), "d"));
    }
}
