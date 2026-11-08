using System.Security.Cryptography;

namespace GoshehYar.Core.Calc;

public sealed record PasswordOptions(bool Lowercase = true, bool Uppercase = true, bool Digits = true, bool Symbols = true, bool ExcludeSimilar = true);

/// <summary>Cryptographically secure password generator. Results are never stored or logged anywhere.</summary>
public static class PasswordGenerator
{
    public const int MinLength = 8;
    public const int MaxLength = 64;
    public const string Similar = "Il1O0o|`";

    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!@#$%^&*()-_=+[]{};:,.?/|`";

    /// <summary>Character sets selected by the options (look-alikes removed when requested). Empty sets are omitted.</summary>
    public static IReadOnlyList<string> Classes(PasswordOptions options)
    {
        var list = new List<string>();
        void Add(bool on, string chars)
        {
            if (!on) return;
            var set = options.ExcludeSimilar ? new string(chars.Where(c => !Similar.Contains(c)).ToArray()) : chars;
            if (set.Length > 0) list.Add(set);
        }

        Add(options.Lowercase, LowerChars);
        Add(options.Uppercase, UpperChars);
        Add(options.Digits, DigitChars);
        Add(options.Symbols, SymbolChars);
        return list;
    }

    /// <summary>Returns an empty string when no character class is selected. Length is clamped to 8..64.</summary>
    public static string Generate(int length, PasswordOptions options)
    {
        var classes = Classes(options);
        if (classes.Count == 0) return string.Empty;

        length = Math.Clamp(length, MinLength, MaxLength);
        var all = string.Concat(classes);
        var chars = new char[length];

        // one guaranteed character from every selected class, the rest from the union
        for (var i = 0; i < length; i++)
        {
            var source = i < classes.Count ? classes[i] : all;
            chars[i] = source[RandomNumberGenerator.GetInt32(source.Length)];
        }

        // Fisher-Yates shuffle so the guaranteed characters are not always first
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
