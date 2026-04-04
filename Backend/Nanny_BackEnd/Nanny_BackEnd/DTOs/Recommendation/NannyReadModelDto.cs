namespace Nanny_BackEnd.DTOs.Recommendation;

/// <summary>Read model dùng để build embedding text cho nanny profile.</summary>
public class NannyReadModelDto
{
    public Guid NannyProfileId { get; set; }
    public Guid UserId { get; set; }

    // Embedding fields
    public int? YearsOfExperience { get; set; }
    public int? EducationLevel { get; set; }
    public string? Bio { get; set; }
    public List<string> SkillNames { get; set; } = new();

    // Cho pipeline
    public List<Guid> SkillIds { get; set; } = new();
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public int? MaxTravelDistance { get; set; }
    public decimal? AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public decimal? Latitude { get; set; }     // từ User
    public decimal? Longitude { get; set; }    // từ User
    public DateOnly? DateOfBirth { get; set; } // từ User

    // Current embedding
    public string? Embedding { get; set; }
    public DateTime? EmbeddingUpdatedAt { get; set; }
}
