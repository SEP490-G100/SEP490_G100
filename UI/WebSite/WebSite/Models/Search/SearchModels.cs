using System.Text.Json.Serialization;

namespace WebSite.Models.Search;

public class SearchJobRequest
{
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? SalaryMin { get; set; }
    public int? JobType { get; set; }
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLng { get; set; }
    public double? MaxLng { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SearchJobResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("parentProfileId")]
    public Guid? ParentProfileId { get; set; }

    [JsonPropertyName("parentUserId")]
    public Guid? ParentUserId { get; set; }

    [JsonPropertyName("childProfileId")]
    public Guid? ChildProfileId { get; set; }

    [JsonPropertyName("isOwner")]
    public bool IsOwner { get; set; }

    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("parentName")]
    public string ParentName { get; set; } = "";

    [JsonPropertyName("jobType")]
    public int JobType { get; set; }

    [JsonPropertyName("salaryMin")]
    public decimal? SalaryMin { get; set; }

    [JsonPropertyName("salaryMax")]
    public decimal? SalaryMax { get; set; }

    [JsonPropertyName("salaryNegotiable")]
    public bool SalaryNegotiable { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("characteristic")]
    public string? Characteristic { get; set; }

    [JsonPropertyName("birthType")]
    public int? BirthType { get; set; }

    [JsonPropertyName("birthTypeLabel")]
    public string? BirthTypeLabel { get; set; }

    [JsonPropertyName("specialNeeds")]
    public string? SpecialNeeds { get; set; }

    [JsonPropertyName("minNannyAge")]
    public int? MinNannyAge { get; set; }

    [JsonPropertyName("maxNannyAge")]
    public int? MaxNannyAge { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = [];

    [JsonPropertyName("scheduleSlots")]
    public List<JobScheduleSlotResponse> ScheduleSlots { get; set; } = [];

    [JsonPropertyName("numberOfChildren")]
    public int? NumberOfChildren { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("moderationStatus")]
    public int ModerationStatus { get; set; }

    [JsonPropertyName("moderationNote")]
    public string? ModerationNote { get; set; }

    [JsonPropertyName("moderatedAt")]
    public DateTime? ModeratedAt { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("distanceKm")]
    public double? DistanceKm { get; set; }

    [JsonPropertyName("subscriptionPlanCode")]
    public string? SubscriptionPlanCode { get; set; }

    [JsonPropertyName("featuredBadge")]
    public bool FeaturedBadge { get; set; }

    [JsonPropertyName("searchPriority")]
    public bool SearchPriority { get; set; }
}

public class JobPostingPrefillResponse
{
    [JsonPropertyName("numberOfChildren")]
    public int? NumberOfChildren { get; set; }

    [JsonPropertyName("selectedChildProfileId")]
    public Guid? SelectedChildProfileId { get; set; }

    [JsonPropertyName("characteristic")]
    public string? Characteristic { get; set; }

    [JsonPropertyName("birthType")]
    public int? BirthType { get; set; }

    [JsonPropertyName("birthTypeLabel")]
    public string? BirthTypeLabel { get; set; }

    [JsonPropertyName("specialNeeds")]
    public string? SpecialNeeds { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = [];

    [JsonPropertyName("children")]
    public List<JobPostingPrefillChildResponse> Children { get; set; } = [];
}

public class JobPostingDetailResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("parentProfileId")]
    public Guid ParentProfileId { get; set; }

    [JsonPropertyName("parentUserId")]
    public Guid ParentUserId { get; set; }

    [JsonPropertyName("childProfileId")]
    public Guid? ChildProfileId { get; set; }

    [JsonPropertyName("parentName")]
    public string ParentName { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("jobType")]
    public int JobType { get; set; }

    [JsonPropertyName("salaryMin")]
    public decimal? SalaryMin { get; set; }

    [JsonPropertyName("salaryMax")]
    public decimal? SalaryMax { get; set; }

    [JsonPropertyName("salaryNegotiable")]
    public bool SalaryNegotiable { get; set; }

    [JsonPropertyName("numberOfChildren")]
    public int? NumberOfChildren { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("characteristic")]
    public string? Characteristic { get; set; }

    [JsonPropertyName("birthType")]
    public int? BirthType { get; set; }

    [JsonPropertyName("birthTypeLabel")]
    public string? BirthTypeLabel { get; set; }

    [JsonPropertyName("specialNeeds")]
    public string? SpecialNeeds { get; set; }

    [JsonPropertyName("minNannyAge")]
    public int? MinNannyAge { get; set; }

    [JsonPropertyName("maxNannyAge")]
    public int? MaxNannyAge { get; set; }

    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = [];

    [JsonPropertyName("scheduleSlots")]
    public List<JobScheduleSlotResponse> ScheduleSlots { get; set; } = [];

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("moderationStatus")]
    public int ModerationStatus { get; set; }

    [JsonPropertyName("moderationNote")]
    public string? ModerationNote { get; set; }

    [JsonPropertyName("moderatedAt")]
    public DateTime? ModeratedAt { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("closedAt")]
    public DateTime? ClosedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("applicationCount")]
    public int ApplicationCount { get; set; }

    [JsonPropertyName("subscriptionPlanCode")]
    public string? SubscriptionPlanCode { get; set; }

    [JsonPropertyName("featuredBadge")]
    public bool FeaturedBadge { get; set; }

    [JsonPropertyName("searchPriority")]
    public bool SearchPriority { get; set; }

    public string StatusLabel => Status switch
    {
        1 => "Public",
        2 => "Hidden",
        _ => "Unknown"
    };

    public string StatusClass => Status switch
    {
        1 => "badge-active",
        2 => "badge-inactive",
        _ => "badge-pending"
    };

    public string ModerationStatusLabel => ModerationStatus switch
    {
        0 => "Pending",
        1 => "Rejected",
        2 => "Approved",
        _ => "Unknown"
    };

    public string ModerationStatusClass => ModerationStatus switch
    {
        0 => "badge-pending",
        1 => "badge-inactive",
        2 => "badge-active",
        _ => "badge-pending"
    };

    public string SalaryLabel => SalaryNegotiable
        ? "Negotiable"
        : $"{SalaryMin?.ToString("N0") ?? "-"} - {SalaryMax?.ToString("N0") ?? "-"} VND";

    public string AgeRangeLabel => MinNannyAge.HasValue || MaxNannyAge.HasValue
        ? $"{MinNannyAge?.ToString() ?? "-"} - {MaxNannyAge?.ToString() ?? "-"}"
        : "Not specified";
}

public class JobPostingPrefillChildResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("characteristic")]
    public string? Characteristic { get; set; }

    [JsonPropertyName("birthType")]
    public int? BirthType { get; set; }

    [JsonPropertyName("birthTypeLabel")]
    public string? BirthTypeLabel { get; set; }

    [JsonPropertyName("specialNeeds")]
    public string? SpecialNeeds { get; set; }
}

public class JobScheduleSlotResponse
{
    [JsonPropertyName("dayOfWeek")]
    public int DayOfWeek { get; set; }

    [JsonPropertyName("timeSlot")]
    public int TimeSlot { get; set; }
}

public class JobSkillOption
{
    [JsonPropertyName("skillId")]
    public Guid SkillId { get; set; }

    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
}

public class SearchApiResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("data")]
    public List<SearchJobResponse> Data { get; set; } = [];
}
