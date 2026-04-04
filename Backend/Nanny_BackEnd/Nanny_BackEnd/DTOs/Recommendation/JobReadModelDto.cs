namespace Nanny_BackEnd.DTOs.Recommendation;

/// <summary>Read model dùng để build embedding text cho job posting.</summary>
public class JobReadModelDto
{
    public Guid JobId { get; set; }

    // Embedding fields
    public string Title { get; set; } = string.Empty;
    public byte? ChildAgeGroup { get; set; }
    public int? NumberOfChildren { get; set; }
    public string? Description { get; set; }
    public string? Characteristic { get; set; }   // từ ChildProfile
    public string? SpecialNeeds { get; set; }      // từ ChildProfile
    public List<string> RequiredSkillNames { get; set; } = new();

    // Cho pipeline
    public List<Guid> RequiredSkillIds { get; set; } = new();
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; }
    public int? MinNannyAge { get; set; }
    public int? MaxNannyAge { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }

    // Current embedding
    public string? Embedding { get; set; }
    public DateTime? EmbeddingUpdatedAt { get; set; }
}
