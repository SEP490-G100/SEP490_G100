using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Report;

public class CreateReportRequest
{
    [Required(ErrorMessage = "Ly do bao cao la bat buoc.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Ly do bao cao phai tu 5 den 500 ky tu.")]
    public string Reason { get; set; } = null!;

    [StringLength(2000, ErrorMessage = "Noi dung bang chung khong duoc vuot qua 2000 ky tu.")]
    public string? Evidence { get; set; }
}

// Backward-compatible DTO names (same shape)
public class CreateJobPostingReportRequest : CreateReportRequest;
public class CreateMessageReportRequest : CreateReportRequest;

public class ReportListItemDto
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public string ReporterName { get; set; } = null!;
    public string ReporterEmail { get; set; } = null!;
    public Guid ReportedEntityId { get; set; }
    public string ReportedEntityType { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string? Evidence { get; set; }
    public int Status { get; set; } // 0 = Pending, 1 = Completed
    public Guid? HandledBy { get; set; }
    public string? HandledByName { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? Resolution { get; set; }
    public string? ActionTaken { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class ReportListResponse
{
    public List<ReportListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

public class ReportDetailDto
{
    public Guid Id { get; set; }
    public Guid ReporterUserId { get; set; }
    public string ReporterName { get; set; } = null!;
    public string ReporterEmail { get; set; } = null!;
    public Guid ReportedEntityId { get; set; }
    public string ReportedEntityType { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string? Evidence { get; set; }
    public int Status { get; set; } // 0 = Pending, 1 = Completed
    public Guid? HandledBy { get; set; }
    public string? HandledByName { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? Resolution { get; set; }
    public string? ActionTaken { get; set; }
    public Guid? OffenderUserId { get; set; }
    public string? OffenderName { get; set; }
    public string? OffenderEmail { get; set; }
    public Guid? ConversationId { get; set; }
    public string? ReportedMessageContent { get; set; }
    public string? JobPostingTitle { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public class ResolveReportRequest
{
    [Required(ErrorMessage = "Resolution is required.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Resolution must be between 1 and 1000 characters.")]
    public string Resolution { get; set; } = null!;

    [Required(ErrorMessage = "ActionTaken is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "ActionTaken must be between 1 and 200 characters.")]
    public string ActionTaken { get; set; } = null!;

    [StringLength(1000, ErrorMessage = "OffenderNotificationMessage must not exceed 1000 characters.")]
    public string? OffenderNotificationMessage { get; set; }
}

public class ToggleReportStatusRequest
{
    public bool IsActive { get; set; } = true;
}
