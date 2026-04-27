using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Subscription;

public class AdminSubscriptionPlanBenefitRequest
{
    [Display(Name = "Giới hạn đăng bài mỗi tháng")]
    [Range(0, 1000)]
    public int MonthlyJobPostLimit { get; set; }

    [Display(Name = "Giới hạn ứng tuyển mỗi tháng")]
    [Range(0, 1000)]
    public int MonthlyApplicationLimit { get; set; }

    public bool FeaturedBadge { get; set; }

    public bool SearchPriority { get; set; }

    [Display(Name = "Số ngày hiển thị bài đăng")]
    [Range(0, 3650)]
    public int ListingDurationDays { get; set; }
}

public class AdminSubscriptionPlanUpsertRequest
{
    [Display(Name = "Tên gói")]
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Display(Name = "Mô tả")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Vai trò mục tiêu")]
    [Required]
    [RegularExpression("^(Parent|Nanny)$", ErrorMessage = "Vai trò mục tiêu chỉ được là Phụ huynh hoặc Bảo mẫu.")]
    public string TargetRole { get; set; } = "";

    [Display(Name = "Giá gói")]
    [Range(typeof(decimal), "0", "999999999")]
    public decimal Price { get; set; }

    [Display(Name = "Thời hạn sử dụng")]
    [Range(1, 3650)]
    public int DurationDays { get; set; }

    [Display(Name = "Thứ tự sắp xếp")]
    [Range(1, 999)]
    public int SortOrder { get; set; } = 1;

    [Display(Name = "Danh sách tính năng")]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 tính năng.")]
    public List<string> Features { get; set; } = [];

    [Display(Name = "Quyền lợi gói")]
    [Required]
    public AdminSubscriptionPlanBenefitRequest Benefits { get; set; } = new();

    public bool CanUseRecommendation { get; set; }
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
    public bool CanUseRecommendation { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminSubscriptionPlanDetailResponse : SubscriptionPlanResponse
{
    public bool IsActive { get; set; }
    public int ActiveSubscriberCount { get; set; }
    public bool CanUseRecommendation { get; set; }
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
