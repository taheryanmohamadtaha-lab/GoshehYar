using System.Text;
using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Localization;

/// <summary>Converts Western digits to Persian digits when the user prefers them.</summary>
public static class DigitFormatter
{
    public static string Format(string text, DigitStyle style)
    {
        if (style != DigitStyle.Persian || string.IsNullOrEmpty(text)) return text;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch is >= '0' and <= '9' ? (char)('۰' + (ch - '0')) : ch);
        }

        return sb.ToString();
    }
}
