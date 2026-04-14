namespace Nanny_BackEnd.DTOs.Recommendation;

/// <summary>Kết quả gợi ý 1 nanny cho một job posting.</summary>
public class NannyRecommendResultDto
{
    public Guid NannyProfileId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public int? EducationLevel { get; set; }
    public decimal? AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public double? DistanceKm { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<NannySkillDto> Skills { get; set; } = new();

    // Hybrid Score breakdown
    public double SemanticScore { get; set; }
    public double SkillScore { get; set; }
    public double SalaryScore { get; set; }
    public double DistanceScore { get; set; }
    public double HybridScore { get; set; }
    public double BusinessBoost { get; set; }
    public double FinalScore { get; set; }
    public bool EmbeddingWasNull { get; set; }
}

/// <summary>Kết quả gợi ý 1 job cho một nanny.</summary>
public class JobRecommendResultDto
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; }
    public double? DistanceKm { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<JobRequiredSkillDto> RequiredSkills { get; set; } = new();

    // Hybrid Score breakdown
    public double SemanticScore { get; set; }
    public double SkillScore { get; set; }
    public double SalaryScore { get; set; }
    public double DistanceScore { get; set; }
    public double HybridScore { get; set; }
    public double BusinessBoost { get; set; }
    public double FinalScore { get; set; }
    public bool EmbeddingWasNull { get; set; }
}

public class NannySkillDto
{
    public string SkillName { get; set; } = string.Empty;
    public int? ProficiencyLevel { get; set; }
}

public class JobRequiredSkillDto
{
    public string SkillName { get; set; } = string.Empty;
    public int? MinProficiencyLevel { get; set; }
}
