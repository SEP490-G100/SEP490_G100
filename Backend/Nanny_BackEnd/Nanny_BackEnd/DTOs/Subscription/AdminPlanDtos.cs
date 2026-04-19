using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.Subscription;

public class AdminCreatePlanRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }

    /// <summary>JSON array of feature strings, e.g. ["Badge noi bat", "Uu tien tim kiem"]</summary>
    public List<string> Features { get; set; } = [];

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class AdminUpdatePlanRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public int? DurationDays { get; set; }
    public List<string>? Features { get; set; }
    public bool? IsActive { get; set; }
    public int? SortOrder { get; set; }
}

public class AdminPlanResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public List<string> Features { get; set; } = [];
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ActiveSubscriberCount { get; set; }
}
