using GoshehYar.Core.Layout;
using GoshehYar.Core.Settings;

namespace GoshehYar.Tests;

public class PanelGeometryTests
{
    private static readonly RectD Screen = new(0, 0, 1920, 1080);

    private static RectD Compute(DockEdge edge, DockAlignment alignment, double reveal, double length = 50) =>
        PanelGeometry.Compute(Screen, edge, alignment, thicknessPx: 56, lengthPercent: length, marginPx: 8, stripPx: 6, reveal);

    private static void AssertRect(RectD expected, RectD actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Width, actual.Width, 3);
        Assert.Equal(expected.Height, actual.Height, 3);
    }

    [Fact]
    public void Right_edge_fully_shown_is_offset_by_margin_and_centered()
    {
        AssertRect(new RectD(1920 - 8 - 56, 270, 56, 540), Compute(DockEdge.Right, DockAlignment.Center, 1));
    }

    [Fact]
    public void Right_edge_hidden_leaves_only_flush_strip()
    {
        AssertRect(new RectD(1920 - 6, 270, 6, 540), Compute(DockEdge.Right, DockAlignment.Center, 0));
    }

    [Fact]
    public void Left_edge_shown_and_hidden()
    {
        AssertRect(new RectD(8, 270, 56, 540), Compute(DockEdge.Left, DockAlignment.Center, 1));
        AssertRect(new RectD(0, 270, 6, 540), Compute(DockEdge.Left, DockAlignment.Center, 0));
    }

    [Fact]
    public void Top_and_bottom_edges_are_horizontal()
    {
        AssertRect(new RectD(480, 8, 960, 56), Compute(DockEdge.Top, DockAlignment.Center, 1));
        AssertRect(new RectD(480, 1080 - 8 - 56, 960, 56), Compute(DockEdge.Bottom, DockAlignment.Center, 1));
        AssertRect(new RectD(480, 1080 - 6, 960, 6), Compute(DockEdge.Bottom, DockAlignment.Center, 0));
    }

    [Fact]
    public void Start_and_end_alignment_give_corner_placement()
    {
        Assert.Equal(8, Compute(DockEdge.Left, DockAlignment.Start, 1).Y, 3);
        Assert.Equal(1080 - 8 - 540, Compute(DockEdge.Left, DockAlignment.End, 1).Y, 3);
        Assert.Equal(8, Compute(DockEdge.Top, DockAlignment.Start, 1).X, 3);
        Assert.Equal(1920 - 8 - 960, Compute(DockEdge.Top, DockAlignment.End, 1).X, 3);
    }

    [Fact]
    public void Halfway_reveal_interpolates_size_and_offset()
    {
        var r = Compute(DockEdge.Right, DockAlignment.Center, 0.5);
        Assert.Equal(6 + (56 - 6) * 0.5, r.Width, 3);
        Assert.Equal(1920 - 4 - r.Width, r.X, 3);
    }

    [Fact]
    public void Length_is_clamped_to_screen_and_to_minimum()
    {
        Assert.Equal(1080 - 16, PanelGeometry.GetShownLength(Screen, DockEdge.Left, 100, 8), 3);
        Assert.Equal(PanelGeometry.MinLength, PanelGeometry.GetShownLength(Screen, DockEdge.Left, 1, 8), 3);
    }

    [Fact]
    public void Reveal_outside_zero_to_one_is_clamped()
    {
        AssertRect(Compute(DockEdge.Right, DockAlignment.Center, 1), Compute(DockEdge.Right, DockAlignment.Center, 5));
        AssertRect(Compute(DockEdge.Right, DockAlignment.Center, 0), Compute(DockEdge.Right, DockAlignment.Center, -3));
    }

    [Fact]
    public void Works_on_a_secondary_monitor_with_offset_origin()
    {
        var second = new RectD(1920, 0, 1280, 1024);
        var r = PanelGeometry.Compute(second, DockEdge.Left, DockAlignment.Center, 56, 50, 8, 6, 1);
        Assert.Equal(1920 + 8, r.X, 3);
        Assert.Equal((1024 - 512) / 2.0, r.Y, 3);
    }
}
