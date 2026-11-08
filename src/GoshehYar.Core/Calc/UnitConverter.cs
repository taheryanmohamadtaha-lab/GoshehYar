namespace GoshehYar.Core.Calc;

public sealed record UnitDef(string Id, double ToBase);

public sealed record UnitCategory(string Id, IReadOnlyList<UnitDef> Units);

/// <summary>
/// Table-driven unit converter. Every unit stores its factor to the category base unit
/// (length m, mass kg, area m2, volume l, speed m/s, time s, data byte). Temperature is handled specially.
/// Data size uses steps of 1024 (1 KB = 1024 bytes, 1 MB = 1024 KB, ...).
/// </summary>
public static class UnitConverter
{
    public const string Temperature = "temperature";

    public static readonly IReadOnlyList<UnitCategory> Categories = new List<UnitCategory>
    {
        new("length", new UnitDef[] { new("mm", 0.001), new("cm", 0.01), new("m", 1), new("km", 1000), new("in", 0.0254), new("ft", 0.3048), new("yd", 0.9144), new("mi", 1609.344) }),
        new("mass", new UnitDef[] { new("mg", 1e-6), new("g", 0.001), new("kg", 1), new("t", 1000), new("oz", 0.028349523125), new("lb", 0.45359237) }),
        new("area", new UnitDef[] { new("cm2", 0.0001), new("m2", 1), new("km2", 1e6), new("ha", 10000), new("ft2", 0.09290304), new("ac", 4046.8564224) }),
        new("volume", new UnitDef[] { new("ml", 0.001), new("l", 1), new("m3", 1000), new("tsp", 0.00492892159375), new("tbsp", 0.01478676478125), new("cup", 0.2365882365), new("gal", 3.785411784) }),
        new(Temperature, new UnitDef[] { new("c", 1), new("f", 1), new("k", 1) }),
        new("speed", new UnitDef[] { new("ms", 1), new("kmh", 1 / 3.6), new("mph", 0.44704), new("kn", 1852 / 3600.0) }),
        new("time", new UnitDef[] { new("sec", 1), new("min", 60), new("hour", 3600), new("day", 86400), new("week", 604800) }),
        new("data", new UnitDef[] { new("B", 1), new("KB", 1024), new("MB", 1048576), new("GB", 1073741824), new("TB", 1099511627776) })
    };

    public static UnitCategory? Find(string categoryId) => Categories.FirstOrDefault(c => c.Id == categoryId);

    /// <summary>Converts a value; null when the category or a unit is unknown or the value is not finite.</summary>
    public static double? Convert(string categoryId, string fromUnit, string toUnit, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return null;
        var category = Find(categoryId);
        if (category == null) return null;

        double result;
        if (categoryId == Temperature)
        {
            double? celsius = fromUnit switch
            {
                "c" => value,
                "f" => (value - 32) * 5 / 9,
                "k" => value - 273.15,
                _ => null
            };
            if (celsius == null) return null;
            double? converted = toUnit switch
            {
                "c" => celsius,
                "f" => celsius * 9 / 5 + 32,
                "k" => celsius + 273.15,
                _ => null
            };
            if (converted == null) return null;
            result = converted.Value;
        }
        else
        {
            var from = category.Units.FirstOrDefault(u => u.Id == fromUnit);
            var to = category.Units.FirstOrDefault(u => u.Id == toUnit);
            if (from == null || to == null) return null;
            result = value * from.ToBase / to.ToBase;
        }

        return double.IsNaN(result) || double.IsInfinity(result) ? null : result;
    }

    /// <summary>Short text with up to 10 significant digits, Western digits, no exponent for ordinary values.</summary>
    public static string FormatValue(double value)
    {
        if (value == 0) return "0";
        var rounded = double.Parse(value.ToString("G10", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
        var abs = Math.Abs(rounded);
        if (abs >= 1e15 || abs < 1e-9) return rounded.ToString("0.#########E+0", System.Globalization.CultureInfo.InvariantCulture);
        var text = ((decimal)rounded).ToString("0.####################", System.Globalization.CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }
}
