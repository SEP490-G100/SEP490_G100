using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Notification;

public class AdminNotificationUpsertRequest : IValidatableObject
{
    [Display(Name = "Tiêu đề")]
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = "";

    [Display(Name = "Nội dung")]
    [Required]
    [StringLength(1000, MinimumLength = 2)]
    public string Content { get; set; } = "";

    [Display(Name = "Đối tượng nhận")]
    [Required]
    [RegularExpression("^(All|Role)$", ErrorMessage = "Đối tượng nhận phải là Tất cả hoặc Theo vai trò.")]
    public string TargetType { get; set; } = "All";

    [Display(Name = "Vai trò nhận thông báo")]
    [StringLength(50)]
    public string? TargetRole { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(TargetType, "Role", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(TargetRole))
            {
                yield return new ValidationResult(
                    "Vai trò nhận thông báo là bắt buộc khi đối tượng nhận là Theo vai trò.",
                    [nameof(TargetRole)]);
                yield break;
            }

            var normalizedRole = TargetRole.Trim();
            if (normalizedRole is not ("Parent" or "Nanny" or "Moderator"))
            {
                yield return new ValidationResult(
                    "Vai trò nhận thông báo chỉ được là Phụ huynh, Bảo mẫu hoặc Điều hành viên.",
                    [nameof(TargetRole)]);
            }
        }
    }
}

public class AdminNotificationListItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string ContentPreview { get; set; } = "";
    public string TargetType { get; set; } = "All";
    public string? TargetRole { get; set; }
    public bool IsDeleted { get; set; }
    public int RecipientCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AdminNotificationDetailResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string TargetType { get; set; } = "All";
    public string? TargetRole { get; set; }
    public bool IsDeleted { get; set; }
    public int RecipientCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}

public class AdminNotificationListResponse
{
    public List<AdminNotificationListItemResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
