namespace WebSite.Models.Admin;

public class AdminPlanDto
{
    public Guid    Id          { get; set; }
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public decimal Price       { get; set; }
    public int     DurationDays { get; set; }
    public bool    IsActive    { get; set; } = true;
    public int     SortOrder   { get; set; }
    public int     ActiveSubscriberCount { get; set; }
    public DateTime CreatedAt  { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> Features { get; set; } = new();

    /// <summary>One feature per line (textarea binding helper).</summary>
    public string? FeaturesRaw
    {
        get => Features.Count > 0 ? string.Join('\n', Features) : null;
        set => Features = (value ?? string.Empty)
            .Split('\n', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
            .ToList();
    }
}
