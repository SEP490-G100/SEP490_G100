namespace Nanny_BackEnd.DTOs.Profile;

public class OnboardingStatusDto
{
    public bool RequiresOnboarding { get; set; }
    public string Role { get; set; } = string.Empty;
    public string NextStep { get; set; } = "Completed";
}

public class UpdateNannyProfileRequest
{
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public int? EducationLevel { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public int? MaxTravelDistance { get; set; }
}

public class NannySkillSelectionDto
{
    public Guid SkillId { get; set; }
    public int? ProficiencyLevel { get; set; }
}

public class SkillSelectionDto
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class UpdateNannySkillsRequest
{
    public List<NannySkillSelectionDto> Skills { get; set; } = new();
}

public class DayAvailabilityDto
{
    public int DayOfWeek { get; set; } // 0-6
    public bool Morning { get; set; }
    public bool Afternoon { get; set; }
    public bool Evening { get; set; }
    public bool Night { get; set; }
}

public class UpdateNannyAvailabilityRequest
{
    public List<DayAvailabilityDto> Days { get; set; } = new();
}

