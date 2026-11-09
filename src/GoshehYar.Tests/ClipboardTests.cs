using GoshehYar.Core.Clipboard;
using Xunit;

namespace GoshehYar.Tests;

public class ClipboardTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "gy-clip-" + Guid.NewGuid().ToString("N") + ".db");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* temp file */ }
    }

    [Fact]
    public void PolicyRejectsEmptyAndHuge()
    {
        Assert.False(ClipboardPolicy.ShouldStore(null));
        Assert.False(ClipboardPolicy.ShouldStore("   "));
        Assert.False(ClipboardPolicy.ShouldStore(new string('x', ClipboardPolicy.MaxChars + 1)));
        Assert.True(ClipboardPolicy.ShouldStore("hello"));
    }

    [Fact]
    public void AddsNewestFirstAndRefusesBadText()
    {
        var s = new ClipboardStore(_path);
        Assert.True(s.Add("one", 10));
        Assert.True(s.Add("two", 10));
        Assert.False(s.Add("  ", 10));
        var all = s.GetAll();
        Assert.Equal(new[] { "two", "one" }, all.Select(e => e.Text).ToArray());
    }

    [Fact]
    public void SameTextMovesToTopWithoutDuplicate()
    {
        var s = new ClipboardStore(_path);
        s.Add("a", 10);
        s.Add("b", 10);
        s.Add("a", 10);
        Assert.Equal(new[] { "a", "b" }, s.GetAll().Select(e => e.Text).ToArray());
    }

    [Fact]
    public void TrimKeepsPinned()
    {
        var s = new ClipboardStore(_path);
        s.Add("keep", 3);
        s.SetPinned(s.GetAll()[0].Id, true);
        for (var i = 0; i < 6; i++) s.Add("t" + i, 3);

        var texts = s.GetAll().Select(e => e.Text).ToList();
        Assert.Equal("keep", texts[0]);                  // pinned first
        Assert.Equal(4, texts.Count);                    // 3 unpinned + 1 pinned
        Assert.DoesNotContain("t0", texts);
        Assert.Contains("t5", texts);
    }

    [Fact]
    public void RecopyKeepsPin()
    {
        var s = new ClipboardStore(_path);
        s.Add("x", 10);
        s.SetPinned(s.GetAll()[0].Id, true);
        s.Add("x", 10);
        Assert.Single(s.GetAll());
        Assert.True(s.GetAll()[0].Pinned);
    }

    [Fact]
    public void SearchDeleteAndClear()
    {
        var s = new ClipboardStore(_path);
        s.Add("Apple pie", 10);
        s.Add("banana", 10);
        s.Add("pinned one", 10);
        s.SetPinned(s.GetAll().First(e => e.Text == "pinned one").Id, true);

        Assert.Single(s.GetAll("APPLE"));
        s.Delete(s.GetAll("banana")[0].Id);
        Assert.Equal(2, s.GetAll().Count);

        s.Clear(keepPinned: true);
        Assert.Single(s.GetAll());
        s.Clear(keepPinned: false);
        Assert.Empty(s.GetAll());
    }
}
