using GoshehYar.Core.Input;

namespace GoshehYar.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void Parses_default_hotkey()
    {
        Assert.True(HotkeyGesture.TryParse("Ctrl+Alt+Space", out var g));
        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, g.Modifiers);
        Assert.Equal(0x20, g.VirtualKey);
    }

    [Fact]
    public void Default_matches_documented_default_text()
    {
        Assert.Equal("Ctrl+Alt+Space", HotkeyGesture.Default.ToString());
    }

    [Theory]
    [InlineData("ctrl + shift + f5", "Ctrl+Shift+F5")]
    [InlineData("Win+Alt+G", "Alt+Win+G")]
    [InlineData("Control+Escape", "Ctrl+Esc")]
    [InlineData("shift+Ctrl+0", "Ctrl+Shift+0")]
    public void Normalizes_text(string input, string expected)
    {
        Assert.True(HotkeyGesture.TryParse(input, out var g));
        Assert.Equal(expected, g.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Space")]            // no modifier
    [InlineData("Ctrl+")]            // missing key
    [InlineData("Ctrl+Alt")]         // modifiers only
    [InlineData("Ctrl+A+B")]         // two keys
    [InlineData("Ctrl+Foo")]         // unknown key
    public void Rejects_invalid_text(string? input)
    {
        Assert.False(HotkeyGesture.TryParse(input, out _));
    }

    [Fact]
    public void Round_trips_through_text()
    {
        var original = new HotkeyGesture(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x71);
        Assert.True(HotkeyGesture.TryParse(original.ToString(), out var parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void Supports_letters_digits_and_function_keys()
    {
        Assert.True(HotkeyGesture.IsSupportedKey('Q'));
        Assert.True(HotkeyGesture.IsSupportedKey('7'));
        Assert.True(HotkeyGesture.IsSupportedKey(0x7B)); // F12
        Assert.False(HotkeyGesture.IsSupportedKey(0xFF));
    }
}
