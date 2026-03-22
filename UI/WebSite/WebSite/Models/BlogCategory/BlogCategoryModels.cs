using System.Text.Json.Serialization;

namespace WebSite.Models.BlogCategory;

public class BlogCategoryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("blogCount")]
    public int BlogCount { get; set; }
}

public class BlogCategoryListResponse
{
    [JsonPropertyName("items")]
    public List<BlogCategoryDto> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Math.Max(PageSize, 1));
}

public class CreateBlogCategoryRequest
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public class UpdateBlogCategoryRequest
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}
