using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Layout;

/// <summary>
/// Pure math for where the floating panel sits. All values are physical pixels.
/// <c>reveal</c> goes from 0 (only the thin handle strip is visible, flush with the edge)
/// to 1 (fully shown, offset from the edge by the margin).
/// </summary>
public static class PanelGeometry
{
    public const double MinLength = 120;

    /// <summary>Length of the panel along the screen edge when fully shown.</summary>
    public static double GetShownLength(RectD area, DockEdge edge, double lengthPercent, double marginPx)
    {
        var vertical = IsVertical(edge);
        var edgeLength = vertical ? area.Height : area.Width;
        var maxLength = Math.Max(1, edgeLength - 2 * marginPx);
        var wanted = edgeLength * lengthPercent / 100.0;
        var min = Math.Min(MinLength, maxLength);
        return Math.Min(maxLength, Math.Max(min, wanted));
    }

    public static RectD Compute(
        RectD area,
        DockEdge edge,
        DockAlignment alignment,
        double thicknessPx,
        double lengthPercent,
        double marginPx,
        double stripPx,
        double reveal)
    {
        reveal = Math.Min(1, Math.Max(0, reveal));

        var vertical = IsVertical(edge);
        var length = GetShownLength(area, edge, lengthPercent, marginPx);

        var strip = Math.Min(stripPx, thicknessPx);
        var thickness = strip + (thicknessPx - strip) * reveal;
        var offset = marginPx * reveal;

        var alongOrigin = vertical ? area.Y : area.X;
        var alongSpace = vertical ? area.Height : area.Width;

        var alongStart = alignment switch
        {
            DockAlignment.Start => alongOrigin + marginPx,
            DockAlignment.End => alongOrigin + alongSpace - marginPx - length,
            _ => alongOrigin + (alongSpace - length) / 2.0
        };

        return edge switch
        {
            DockEdge.Left => new RectD(area.X + offset, alongStart, thickness, length),
            DockEdge.Right => new RectD(area.Right - offset - thickness, alongStart, thickness, length),
            DockEdge.Top => new RectD(alongStart, area.Y + offset, length, thickness),
            _ => new RectD(alongStart, area.Bottom - offset - thickness, length, thickness)
        };
    }

    public static bool IsVertical(DockEdge edge) => edge is DockEdge.Left or DockEdge.Right;
}
