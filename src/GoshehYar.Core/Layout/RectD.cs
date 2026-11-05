namespace GoshehYar.Core.Layout;

/// <summary>Simple rectangle in physical pixels (kept free of WPF/WinForms types so it is unit-testable).</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}
