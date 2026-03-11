namespace Nanny_BackEnd.DTOs.Search;

public class SearchJobResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int JobType { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public int SalaryType { get; set; }
    public bool SalaryNegotiable { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Location { get; set; }
    public int? NumberOfChildren { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<string> RequiredSkills { get; set; } = [];
}
