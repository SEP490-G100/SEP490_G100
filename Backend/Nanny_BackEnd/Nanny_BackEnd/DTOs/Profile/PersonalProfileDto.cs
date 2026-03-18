namespace Nanny_BackEnd.DTOs.Profile;

public class PersonalProfileDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Gender { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public List<string> Roles { get; set; } = new();

    // Parent fields
    public string? FamilyDescription { get; set; }
    public int? NumberOfChildren { get; set; }
    public string? SpecialNeeds { get; set; }
    public string? Notes { get; set; }
    public string? Characteristic { get; set; }
    public byte? ChildAgeGroup { get; set; }
    public List<ChildProfileDto>? Children { get; set; }

    // Nanny fields
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public int? EducationLevel { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public int? MaxTravelDistance { get; set; }
    public string? VerificationStatus { get; set; }
    public decimal? AverageRating { get; set; }
    public int? TotalReviews { get; set; }
    public List<NannySkillItemDto>? Skills { get; set; }
    public List<NannyAvailabilityItemDto>? Availabilities { get; set; }
}

public class NannySkillItemDto
{
    public Guid SkillId { get; set; }
    public int? ProficiencyLevel { get; set; }
}

public class NannyAvailabilityItemDto
{
    public int DayOfWeek { get; set; }
    public bool IsAvailable { get; set; }
    public int TimeSlot { get; set; }
}
