namespace Nanny_BackEnd.DTOs.Search;

public class SearchJobRequest
{
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? SalaryMin { get; set; }
    public int? JobType { get; set; }   // 1=FullTime 2=PartTime 3=Overnight
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // Tọa độ của Nanny (tuỳ chọn) — dùng để tính DistanceKm
    public double? NannyLat { get; set; }
    public double? NannyLng { get; set; }
}
