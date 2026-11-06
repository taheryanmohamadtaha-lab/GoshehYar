namespace GoshehYar.Core.Notes;

public sealed class ChecklistItem
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsChecklist { get; set; }
    public List<ChecklistItem> Items { get; set; } = new();
    public string Category { get; set; } = "";

    /// <summary>Color key: "" (none), yellow, green, blue, pink, purple, orange.</summary>
    public string Color { get; set; } = "";
    public bool Pinned { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReminderUtc { get; set; }
    public bool ReminderFired { get; set; }

    public static readonly string[] ColorKeys = { "", "yellow", "green", "blue", "pink", "purple", "orange" };
}
