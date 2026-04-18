using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Notification;

public class AdminNotificationUpsertRequest : IValidatableObject
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(1000, MinimumLength = 2)]
    public string Content { get; set; } = "";

    [Required]
    [RegularExpression("^(All|Role)$", ErrorMessage = "TargetType must be All or Role.")]
    public string TargetType { get; set; } = "All";

    [StringLength(50)]
    public string? TargetRole { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(TargetType, "Role", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(TargetRole))
            {
                yield return new ValidationResult(
                    "TargetRole is required when TargetType is Role.",
                    [nameof(TargetRole)]);
                yield break;
            }

            var normalizedRole = TargetRole.Trim();
            if (normalizedRole is not ("Parent" or "Nanny" or "Moderator"))
            {
                yield return new ValidationResult(
                    "TargetRole must be Parent, Nanny, or Moderator.",
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
