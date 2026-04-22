namespace Nanny_BackEnd.DTOs.Recommendation;

public class NannyCandidate
{
    public Guid NannyProfileId { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public int? EducationLevel { get; set; }
    public decimal? AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int? MaxTravelDistance { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public List<Guid> SkillIds { get; set; } = new();
    public string? Embedding { get; set; }
    public List<NannySkillDto> Skills { get; set; } = new();
}

public class JobCandidate
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public List<Guid> RequiredSkillIds { get; set; } = new();
    public string? Embedding { get; set; }
    public List<JobRequiredSkillDto> RequiredSkills { get; set; } = new();
}
