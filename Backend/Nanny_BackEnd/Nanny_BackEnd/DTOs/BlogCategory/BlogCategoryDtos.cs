using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.BlogCategory;

public class BlogCategoryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = null!;

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
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Name không được để trống.")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("slug")]
    [Required(ErrorMessage = "Slug không được để trống.")]
    public string Slug { get; set; } = null!;
}

public class UpdateBlogCategoryRequest
{
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Name không được để trống.")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("slug")]
    [Required(ErrorMessage = "Slug không được để trống.")]
    public string Slug { get; set; } = null!;
}
