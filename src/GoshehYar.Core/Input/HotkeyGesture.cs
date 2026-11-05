using System.Diagnostics.CodeAnalysis;

namespace GoshehYar.Core.Input;

/// <summary>Modifier flags. Values intentionally match the Win32 MOD_* constants.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8
}

/// <summary>A global hotkey: modifiers + one Win32 virtual-key code. Text form: "Ctrl+Alt+Space".</summary>
public sealed class HotkeyGesture : IEquatable<HotkeyGesture>
{
    private static readonly Dictionary<int, string> KeyNames = BuildKeyNames();
    private static readonly Dictionary<string, int> KeyCodes = BuildKeyCodes();

    public HotkeyGesture(HotkeyModifiers modifiers, int virtualKey)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }

    public HotkeyModifiers Modifiers { get; }
    public int VirtualKey { get; }

    public bool HasModifier => Modifiers != HotkeyModifiers.None;

    public static HotkeyGesture Default => new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x20);

    /// <summary>True if the virtual key is one this app supports for hotkeys.</summary>
    public static bool IsSupportedKey(int virtualKey) => KeyNames.ContainsKey(virtualKey);

    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var modifiers = HotkeyModifiers.None;
        int? key = null;

        foreach (var rawPart in text.Split('+'))
        {
            var part = rawPart.Trim();
            if (part.Length == 0) return false;

            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifiers.Ctrl;
                    break;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    if (key is not null) return false; // only one non-modifier key
                    if (!KeyCodes.TryGetValue(part, out var code)) return false;
                    key = code;
                    break;
            }
        }

        if (key is null || modifiers == HotkeyModifiers.None) return false;

        gesture = new HotkeyGesture(modifiers, key.Value);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyNames.TryGetValue(VirtualKey, out var name) ? name : $"0x{VirtualKey:X2}");
        return string.Join("+", parts);
    }

    public bool Equals(HotkeyGesture? other) =>
        other is not null && other.Modifiers == Modifiers && other.VirtualKey == VirtualKey;

    public override bool Equals(object? obj) => Equals(obj as HotkeyGesture);

    public override int GetHashCode() => HashCode.Combine((int)Modifiers, VirtualKey);

    private static Dictionary<int, string> BuildKeyNames()
    {
        var map = new Dictionary<int, string>
        {
            [0x08] = "Backspace",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x1B] = "Esc",
            [0x20] = "Space",
            [0x21] = "PageUp",
            [0x22] = "PageDown",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Left",
            [0x26] = "Up",
            [0x27] = "Right",
            [0x28] = "Down",
            [0x2D] = "Insert",
            [0x2E] = "Delete"
        };

        for (var c = 'A'; c <= 'Z'; c++) map[c] = c.ToString();
        for (var d = '0'; d <= '9'; d++) map[d] = d.ToString();
        for (var i = 1; i <= 24; i++) map[0x70 + i - 1] = "F" + i;

        return map;
    }

    private static Dictionary<string, int> BuildKeyCodes()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in KeyNames) map[pair.Value] = pair.Key;
        map["Escape"] = 0x1B;
        map["Return"] = 0x0D;
        map["Del"] = 0x2E;
        map["Ins"] = 0x2D;
        map["PgUp"] = 0x21;
        map["PgDn"] = 0x22;
        return map;
    }
}
