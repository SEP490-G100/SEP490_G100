using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebSite.Models.Admin;

public class AdminNotificationListItemViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("contentPreview")]
    public string ContentPreview { get; set; } = "";

    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = "All";

    [JsonPropertyName("targetRole")]
    public string? TargetRole { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("recipientCount")]
    public int RecipientCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public class AdminNotificationListResponse
{
    [JsonPropertyName("items")]
    public List<AdminNotificationListItemViewModel> Items { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}

public class AdminNotificationDetailViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = "All";

    [JsonPropertyName("targetRole")]
    public string? TargetRole { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("recipientCount")]
    public int RecipientCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public class AdminNotificationFormViewModel : IValidatableObject
{
    public Guid? Id { get; set; }
    public bool IsDeleted { get; set; }
    public int RecipientCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(1000, MinimumLength = 2)]
    public string Content { get; set; } = "";

    public string TargetType { get; set; } = "All";

    [StringLength(50)]
    public string? TargetRole { get; set; } = "All";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TargetType == "Role" && string.IsNullOrWhiteSpace(TargetRole))
        {
            yield return new ValidationResult(
                "Please choose a target role.",
                [nameof(TargetRole)]);
        }
    }

    public static AdminNotificationFormViewModel FromDetail(AdminNotificationDetailViewModel detail) => new()
    {
        Id = detail.Id,
        IsDeleted = detail.IsDeleted,
        RecipientCount = detail.RecipientCount,
        CreatedAt = detail.CreatedAt,
        UpdatedAt = detail.UpdatedAt,
        Title = detail.Title,
        Content = detail.Content,
        TargetType = detail.TargetType,
        TargetRole = detail.TargetType == "Role" ? detail.TargetRole : "All"
    };
}
