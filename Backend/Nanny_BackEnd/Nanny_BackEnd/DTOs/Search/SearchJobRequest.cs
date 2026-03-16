namespace Nanny_BackEnd.DTOs.Search;

public class SearchJobRequest
{
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? SalaryMin { get; set; }
    public int? JobType { get; set; }  
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public double? NannyLat { get; set; }
    public double? NannyLng { get; set; }
}
