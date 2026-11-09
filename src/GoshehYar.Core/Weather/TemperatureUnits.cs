using System.Globalization;

namespace GoshehYar.Core.Weather;

public static class TemperatureUnits
{
    public static double ToFahrenheit(double celsius) => celsius * 9.0 / 5.0 + 32.0;

    /// <summary>"23°" or "73°" (Western digits, rounded). Missing value gives "--".</summary>
    public static string Format(double? celsius, bool fahrenheit)
    {
        if (celsius is null || double.IsNaN(celsius.Value) || double.IsInfinity(celsius.Value)) return "--";
        var v = fahrenheit ? ToFahrenheit(celsius.Value) : celsius.Value;
        var rounded = (int)Math.Round(v, MidpointRounding.AwayFromZero);
        if (rounded == 0) rounded = 0; // avoid "-0"
        return rounded.ToString(CultureInfo.InvariantCulture) + "°";
    }

    public static string UnitLetter(bool fahrenheit) => fahrenheit ? "F" : "C";
}
