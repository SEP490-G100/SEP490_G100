using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebSite.Models.Admin;

public class AdminSubscriptionPlanBenefitViewModel
{
    [JsonPropertyName("monthlyJobPostLimit")]
    [Range(0, 1000)]
    public int MonthlyJobPostLimit { get; set; }

    [JsonPropertyName("monthlyApplicationLimit")]
    [Range(0, 1000)]
    public int MonthlyApplicationLimit { get; set; }

    [JsonPropertyName("featuredBadge")]
    public bool FeaturedBadge { get; set; }

    [JsonPropertyName("searchPriority")]
    public bool SearchPriority { get; set; }

    [JsonPropertyName("listingDurationDays")]
    [Range(0, 3650)]
    public int ListingDurationDays { get; set; }
}

public class AdminSubscriptionPlanListItemViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("targetRole")]
    public string TargetRole { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("durationDays")]
    public int DurationDays { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("featureCount")]
    public int FeatureCount { get; set; }

    [JsonPropertyName("activeSubscriberCount")]
    public int ActiveSubscriberCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class AdminSubscriptionPlanListResponse
{
    [JsonPropertyName("items")]
    public List<AdminSubscriptionPlanListItemViewModel> Items { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}

public class AdminSubscriptionPlanDetailViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("targetRole")]
    public string TargetRole { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("durationDays")]
    public int DurationDays { get; set; }

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = [];

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("benefits")]
    public AdminSubscriptionPlanBenefitViewModel Benefits { get; set; } = new();

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("activeSubscriberCount")]
    public int ActiveSubscriberCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public class AdminSubscriptionPlanFormViewModel
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int ActiveSubscriberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, ErrorMessage = "Description must be at most 500 characters.")]
    public string Description { get; set; } = "";

    [Required]
    [RegularExpression("^(Parent|Nanny)$", ErrorMessage = "Target role must be Parent or Nanny.")]
    public string TargetRole { get; set; } = "Parent";

    [Range(typeof(decimal), "1000", "999999999")]
    public decimal Price { get; set; }

    [Range(1, 3650)]
    public int DurationDays { get; set; }

    [Range(1, 999)]
    public int SortOrder { get; set; } = 1;

    [Required]
    public string FeatureLines { get; set; } = "";

    [Range(0, 1000)]
    public int MonthlyJobPostLimit { get; set; }

    [Range(0, 1000)]
    public int MonthlyApplicationLimit { get; set; }

    public bool FeaturedBadge { get; set; }

    public bool SearchPriority { get; set; }

    [Range(0, 3650)]
    public int ListingDurationDays { get; set; }

    public List<string> GetFeatures() =>
        FeatureLines
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static AdminSubscriptionPlanFormViewModel FromDetail(AdminSubscriptionPlanDetailViewModel detail) => new()
    {
        Id = detail.Id,
        Code = detail.Code,
        IsActive = detail.IsActive,
        ActiveSubscriberCount = detail.ActiveSubscriberCount,
        CreatedAt = detail.CreatedAt,
        UpdatedAt = detail.UpdatedAt,
        Name = detail.Name,
        Description = detail.Description ?? "",
        TargetRole = detail.TargetRole,
        Price = detail.Price,
        DurationDays = detail.DurationDays,
        SortOrder = detail.SortOrder,
        FeatureLines = string.Join(Environment.NewLine, detail.Features),
        MonthlyJobPostLimit = detail.Benefits.MonthlyJobPostLimit,
        MonthlyApplicationLimit = detail.Benefits.MonthlyApplicationLimit,
        FeaturedBadge = detail.Benefits.FeaturedBadge,
        SearchPriority = detail.Benefits.SearchPriority,
        ListingDurationDays = detail.Benefits.ListingDurationDays
    };
}
