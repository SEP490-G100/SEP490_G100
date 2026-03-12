namespace Nanny_BackEnd.DTOs.JobPosting;

public class JobPostingDetailResponse
{
    public Guid Id { get; set; }
    public Guid ParentProfileId { get; set; }
    public string ParentName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int JobType { get; set; }
    public decimal? SalaryMin { get; set; }
 
    public bool SalaryNegotiable { get; set; }
    public int? NumberOfChildren { get; set; }
    public string? Location { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int Status { get; set; }              // 0=Draft 1=Active 2=Closed
    public int ModerationStatus { get; set; }    // 0=Pending 1=Rejected 2=Approved
    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ApplicationCount { get; set; }
}
