using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Subscription;

public class AdminSubscriptionPlanBenefitRequest
{
    [Range(0, 1000)]
    public int MonthlyJobPostLimit { get; set; }

    [Range(0, 1000)]
    public int MonthlyApplicationLimit { get; set; }

    public bool FeaturedBadge { get; set; }

    public bool SearchPriority { get; set; }

    [Range(0, 3650)]
    public int ListingDurationDays { get; set; }
}

public class AdminSubscriptionPlanUpsertRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [RegularExpression("^(Parent|Nanny)$", ErrorMessage = "TargetRole chi duoc la Parent hoac Nanny.")]
    public string TargetRole { get; set; } = "";

    [Range(typeof(decimal), "1000", "999999999")]
    public decimal Price { get; set; }

    [Range(1, 3650)]
    public int DurationDays { get; set; }

    [Range(1, 999)]
    public int SortOrder { get; set; } = 1;

    [MinLength(1, ErrorMessage = "Phai co it nhat 1 feature.")]
    public List<string> Features { get; set; } = [];

    [Required]
    public AdminSubscriptionPlanBenefitRequest Benefits { get; set; } = new();
}

public class AdminSubscriptionPlanStatusRequest
{
    public bool IsActive { get; set; }
}

public class AdminSubscriptionPlanListItemResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string TargetRole { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int FeatureCount { get; set; }
    public int ActiveSubscriberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminSubscriptionPlanDetailResponse : SubscriptionPlanResponse
{
    public bool IsActive { get; set; }
    public int ActiveSubscriberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AdminSubscriptionPlanListResponse
{
    public List<AdminSubscriptionPlanListItemResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
