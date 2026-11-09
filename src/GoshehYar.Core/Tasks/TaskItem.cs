namespace GoshehYar.Core.Tasks;

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>One to-do task. Due is a local wall-clock time (kind Unspecified); without DueHasTime only its date counts.</summary>
public sealed class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Note { get; set; } = "";
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public string Category { get; set; } = "";
    public DateTime? Due { get; set; }
    public bool DueHasTime { get; set; }
    public bool Done { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Minutes before the due moment (0 = at due time); null = no reminder.</summary>
    public int? ReminderOffsetMinutes { get; set; }

    public DateTime? ReminderUtc { get; set; }
    public bool ReminderFired { get; set; }
}
