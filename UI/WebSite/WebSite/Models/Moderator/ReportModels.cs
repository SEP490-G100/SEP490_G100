using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Moderator;

public class ModeratorReportListItemDto
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public string ReporterName { get; set; } = "";
    public string ReporterEmail { get; set; } = "";
    public Guid ReportedEntityId { get; set; }
    public string ReportedEntityType { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? Evidence { get; set; }
    public int Status { get; set; } // 0 = Pending, 1 = Completed
    public Guid? HandledBy { get; set; }
    public string? HandledByName { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? Resolution { get; set; }
    public string? ActionTaken { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public bool IsActive => !IsDeleted;
    public string StatusLabel => Status == 1 ? "Completed" : "Pending";
    public string StatusClass => Status == 1 ? "badge-active" : "badge-pending";
    public string EntityTypeLabel => ReportedEntityType switch
    {
        "JobPosting" => "Job Posting",
        "Conversation" => "Conversation",
        "Message" => "Message",
        "Profile" => "Profile",
        _ => ReportedEntityType
    };
    public string ReporterDisplay => string.IsNullOrWhiteSpace(ReporterName) ? ReporterEmail : ReporterName;
}

public class ModeratorReportListResponse
{
    public List<ModeratorReportListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class ModeratorReportDetailDto
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public string ReporterName { get; set; } = "";
    public string ReporterEmail { get; set; } = "";
    public Guid ReportedEntityId { get; set; }
    public string ReportedEntityType { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? Evidence { get; set; }
    public int Status { get; set; } // 0 = Pending, 1 = Completed
    public Guid? HandledBy { get; set; }
    public string? HandledByName { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? Resolution { get; set; }
    public string? ActionTaken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string StatusLabel => Status == 1 ? "Completed" : "Pending";
    public string StatusClass => Status == 1 ? "badge-active" : "badge-pending";
    public string EntityTypeLabel => ReportedEntityType switch
    {
        "JobPosting" => "Job Posting",
        "Conversation" => "Conversation",
        "Message" => "Message",
        "Profile" => "Profile",
        _ => ReportedEntityType
    };
    public string ReporterDisplay => string.IsNullOrWhiteSpace(ReporterName) ? ReporterEmail : ReporterName;
}

public class ModeratorResolveReportRequest
{
    [Required(ErrorMessage = "Resolution is required.")]
    [StringLength(1000, ErrorMessage = "Resolution must not exceed 1000 characters.")]
    public string Resolution { get; set; } = "";

    [Required(ErrorMessage = "Action Taken is required.")]
    [StringLength(200, ErrorMessage = "Action Taken must not exceed 200 characters.")]
    public string ActionTaken { get; set; } = "";
}

public class ModeratorReportDetailPageModel
{
    public ModeratorReportDetailDto Detail { get; set; } = new();
    public ModeratorResolveReportRequest Form { get; set; } = new();
}
