namespace Nanny_BackEnd.DTOs.Search;

public class SearchJobResponse
{
    public Guid Id { get; set; }           // Cần cho Edit/Delete trên UI
    public Guid? ParentProfileId { get; set; } // Dùng cho link xem hồ sơ
    public bool IsOwner { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ParentName { get; set; } = "";
    public int JobType { get; set; }
    public decimal? SalaryMin { get; set; }
    public bool SalaryNegotiable { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Location { get; set; }
    public int? NumberOfChildren { get; set; }
    public double? Latitude { get; set; }  // Cần cho Map pin
    public double? Longitude { get; set; }
    public int Status { get; set; }        // 0=Draft, 1=Active
    public DateTime? PublishedAt { get; set; }
    public double? DistanceKm { get; set; }
}
