using System.ComponentModel.DataAnnotations;
using WebSite.Models.Profile;
using WebSite.Models.Search;

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
    public string StatusLabel => Status == 1 ? "Đã xử lý" : "Chờ xử lý";
    public string StatusClass => Status == 1 ? "badge-active" : "badge-pending";
    public string EntityTypeLabel => ReportedEntityType switch
    {
        "JobPosting" => "Bài đăng",
        "Conversation" => "Cuộc trò chuyện",
        "Message" => "Tin nhắn",
        "Profile" => "Hồ sơ",
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
    public Guid? OffenderUserId { get; set; }
    public string? OffenderName { get; set; }
    public string? OffenderEmail { get; set; }
    public Guid? ConversationId { get; set; }
    public string? ReportedMessageContent { get; set; }
    public string? JobPostingTitle { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public string StatusLabel => Status == 1 ? "Đã xử lý" : "Chờ xử lý";
    public string StatusClass => Status == 1 ? "badge-active" : "badge-pending";
    public string EntityTypeLabel => ReportedEntityType switch
    {
        "JobPosting" => "Bài đăng",
        "Conversation" => "Cuộc trò chuyện",
        "Message" => "Tin nhắn",
        "Profile" => "Hồ sơ",
        _ => ReportedEntityType
    };
    public string ReporterDisplay => string.IsNullOrWhiteSpace(ReporterName) ? ReporterEmail : ReporterName;
}

public class ModeratorResolveReportRequest
{
    [Display(Name = "Kết quả xử lý")]
    [Required(ErrorMessage = "Vui lòng nhập kết quả xử lý.")]
    [StringLength(1000, ErrorMessage = "Kết quả xử lý không được vượt quá 1000 ký tự.")]
    public string Resolution { get; set; } = "";

    [Display(Name = "Hành động đã thực hiện")]
    [Required(ErrorMessage = "Vui lòng nhập hành động đã thực hiện.")]
    [StringLength(200, ErrorMessage = "Hành động đã thực hiện không được vượt quá 200 ký tự.")]
    public string ActionTaken { get; set; } = "";

    [Display(Name = "Nội dung thông báo cho người vi phạm")]
    [StringLength(1000, ErrorMessage = "Nội dung thông báo cho người vi phạm không được vượt quá 1000 ký tự.")]
    public string? OffenderNotificationMessage { get; set; }
}

public class ModeratorReportDetailPageModel
{
    public ModeratorReportDetailDto Detail { get; set; } = new();
    public ModeratorResolveReportRequest Form { get; set; } = new();
}

public class ModeratorReportedJobPostingDetailPageModel
{
    public Guid? ReportId { get; set; }
    public JobPostingDetailResponse JobPosting { get; set; } = new();
}

public class ModeratorReportedProfileDetailPageModel
{
    public Guid? ReportId { get; set; }
    public PersonalProfileViewModel Profile { get; set; } = new();
}
