using System.Text.Json.Serialization;

namespace WebSite.Models.FAQ;

public class FaqDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("question")]
    public string Question { get; set; } = null!;

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = null!;

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("viewCount")]
    public int ViewCount { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public class FaqListResponse
{
    [JsonPropertyName("items")]
    public List<FaqDto> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Math.Max(PageSize, 1));
}

/// <summary>Used by CreateFAQ form. SortOrder is NOT here — auto-assigned by backend.</summary>
public class CreateFaqRequest
{
    public string Question { get; set; } = string.Empty;
    public string Answer   { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive   { get; set; } = true;
}

/// <summary>Used by ViewFAQDetail update form. SortOrder is NOT updatable.</summary>
public class UpdateFaqRequest
{
    public string Question { get; set; } = string.Empty;
    public string Answer   { get; set; } = string.Empty;
    public bool IsActive   { get; set; } = true;
    public bool IsDeleted  { get; set; } = false;
}
