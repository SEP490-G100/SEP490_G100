using System.Text.Json.Serialization;

namespace WebSite.Models.Nanny;

public class NannyBrowsePageViewModel
{
    public List<NannySkillOptionViewModel> SkillOptions { get; set; } = new();
}

public class NannySearchRequestViewModel
{
    public string? Keyword { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int? MinExperience { get; set; }
    public decimal? MinExpectedSalary { get; set; }
    public decimal? MaxExpectedSalary { get; set; }
    public int? VerificationStatus { get; set; }
    public int? DayOfWeek { get; set; }
    public int? TimeSlot { get; set; }
    public string? SkillIds { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class NannyBrowseApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<NannyListItemViewModel> Data { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

public class NannyDetailApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public NannyDetailViewModel? Data { get; set; }
}

public class NannyListItemViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("isFavorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("age")]
    public int? Age { get; set; }

    [JsonPropertyName("yearsOfExperience")]
    public int? YearsOfExperience { get; set; }

    [JsonPropertyName("expectedSalaryMin")]
    public decimal? ExpectedSalaryMin { get; set; }

    [JsonPropertyName("expectedSalaryMax")]
    public decimal? ExpectedSalaryMax { get; set; }

    [JsonPropertyName("averageRating")]
    public decimal? AverageRating { get; set; }

    [JsonPropertyName("totalReviews")]
    public int TotalReviews { get; set; }

    [JsonPropertyName("verificationStatus")]
    public int VerificationStatus { get; set; }

    [JsonPropertyName("verificationStatusLabel")]
    public string VerificationStatusLabel { get; set; } = "";

    [JsonPropertyName("educationLevel")]
    public int? EducationLevel { get; set; }

    [JsonPropertyName("educationLevelLabel")]
    public string? EducationLevelLabel { get; set; }

    [JsonPropertyName("skills")]
    public List<NannySkillOptionViewModel> Skills { get; set; } = new();

    [JsonPropertyName("availabilitySlots")]
    public List<NannyAvailabilitySlotViewModel> AvailabilitySlots { get; set; } = new();

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
}

public class NannyDetailViewModel : NannyListItemViewModel
{
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("ward")]
    public string? Ward { get; set; }

    [JsonPropertyName("maxTravelDistance")]
    public int? MaxTravelDistance { get; set; }

    [JsonPropertyName("profileCompleteness")]
    public int ProfileCompleteness { get; set; }

    [JsonPropertyName("verifiedAt")]
    public DateTime? VerifiedAt { get; set; }
}

public class NannySkillOptionViewModel
{
    [JsonPropertyName("skillId")]
    public Guid SkillId { get; set; }

    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("proficiencyLevel")]
    public int? ProficiencyLevel { get; set; }

    [JsonPropertyName("proficiencyLevelLabel")]
    public string? ProficiencyLevelLabel { get; set; }
}

public class NannyAvailabilitySlotViewModel
{
    [JsonPropertyName("dayOfWeek")]
    public int DayOfWeek { get; set; }

    [JsonPropertyName("dayLabel")]
    public string DayLabel { get; set; } = "";

    [JsonPropertyName("timeSlot")]
    public int TimeSlot { get; set; }

    [JsonPropertyName("timeSlotLabel")]
    public string TimeSlotLabel { get; set; } = "";
}
