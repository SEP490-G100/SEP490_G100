using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Report;

public class CreateReportRequest
{
    [Display(Name = "Lý do báo cáo")]
    [Required(ErrorMessage = "Lý do báo cáo là bắt buộc.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do báo cáo phải từ 5 đến 500 ký tự.")]
    public string Reason { get; set; } = null!;

    [Display(Name = "Nội dung bằng chứng")]
    [StringLength(2000, ErrorMessage = "Nội dung bằng chứng không được vượt quá 2000 ký tự.")]
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
    [Display(Name = "Kết quả xử lý")]
    [Required(ErrorMessage = "Vui lòng nhập kết quả xử lý.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Kết quả xử lý phải từ 1 đến 1000 ký tự.")]
    public string Resolution { get; set; } = null!;

    [Display(Name = "Hành động đã thực hiện")]
    [Required(ErrorMessage = "Vui lòng nhập hành động đã thực hiện.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Hành động đã thực hiện phải từ 1 đến 200 ký tự.")]
    public string ActionTaken { get; set; } = null!;

    [Display(Name = "Nội dung thông báo cho người vi phạm")]
    [StringLength(1000, ErrorMessage = "Nội dung thông báo cho người vi phạm không được vượt quá 1000 ký tự.")]
    public string? OffenderNotificationMessage { get; set; }
}

public class ToggleReportStatusRequest
{
    public bool IsActive { get; set; } = true;
}
