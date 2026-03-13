using System.Text.Json.Serialization;

namespace WebSite.Models.Search;

public class SearchJobRequest
{
    public string? City { get; set; }
    public string? District { get; set; }
    public decimal? SalaryMin { get; set; }
    public int? JobType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SearchJobResponse
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("parentProfileId")]
    public Guid? ParentProfileId { get; set; }

    [JsonPropertyName("isOwner")]
    public bool IsOwner { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("parentName")]
    public string ParentName { get; set; } = "";

    [JsonPropertyName("jobType")]
    public int JobType { get; set; }

    [JsonPropertyName("salaryMin")]
    public decimal? SalaryMin { get; set; }

    [JsonPropertyName("salaryMax")]
    public decimal? SalaryMax { get; set; }

    [JsonPropertyName("salaryType")]
    public int SalaryType { get; set; }

    [JsonPropertyName("salaryNegotiable")]
    public bool SalaryNegotiable { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("numberOfChildren")]
    public int? NumberOfChildren { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("distanceKm")]
    public double? DistanceKm { get; set; }
}

public class SearchApiResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("data")]
    public List<SearchJobResponse> Data { get; set; } = [];
}
