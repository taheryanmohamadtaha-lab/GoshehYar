using System.Text;

namespace GoshehYar.Core.Notes;

/// <summary>Filtering, ordering and export of notes (pure logic, no storage).</summary>
public static class NoteTools
{
    /// <summary>Pinned first, then newest edit first. Query matches title, body, checklist items and category.</summary>
    public static IReadOnlyList<Note> Filter(IEnumerable<Note> notes, string? query, string? category)
    {
        var q = (query ?? "").Trim();
        var cat = (category ?? "").Trim();

        return notes
            .Where(n => cat.Length == 0 || string.Equals(n.Category, cat, StringComparison.OrdinalIgnoreCase))
            .Where(n => q.Length == 0 || Matches(n, q))
            .OrderByDescending(n => n.Pinned)
            .ThenByDescending(n => n.ModifiedUtc)
            .ToList();
    }

    public static IReadOnlyList<string> Categories(IEnumerable<Note> notes) =>
        notes.Select(n => n.Category.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static bool Matches(Note n, string q)
    {
        var c = StringComparison.CurrentCultureIgnoreCase;
        return n.Title.Contains(q, c)
               || n.Body.Contains(q, c)
               || n.Category.Contains(q, c)
               || n.Items.Any(i => i.Text.Contains(q, c));
    }

    public static string ToText(Note n)
    {
        var sb = new StringBuilder();
        if (n.Title.Length > 0) sb.AppendLine(n.Title).AppendLine(new string('-', Math.Min(40, Math.Max(3, n.Title.Length))));
        AppendBody(sb, n, "[x] ", "[ ] ");
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public static string ToMarkdown(Note n)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# " + (n.Title.Length > 0 ? n.Title : "Untitled")).AppendLine();
        AppendBody(sb, n, "- [x] ", "- [ ] ");
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendBody(StringBuilder sb, Note n, string done, string todo)
    {
        if (n.IsChecklist)
        {
            foreach (var i in n.Items) sb.AppendLine((i.Done ? done : todo) + i.Text);
        }
        else
        {
            sb.AppendLine(n.Body);
        }
    }

    public static string SafeFileName(string name, string fallback = "note")
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((name ?? "").Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        if (cleaned.Length > 60) cleaned = cleaned[..60].Trim();
        return cleaned.Length == 0 ? fallback : cleaned;
    }
}
