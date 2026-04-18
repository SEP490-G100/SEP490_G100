using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.Blog;

public class BlogDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = null!;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("authorUserId")]
    public Guid AuthorUserId { get; set; }

    [JsonPropertyName("authorName")]
    public string? AuthorName { get; set; }

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("viewCount")]
    public int ViewCount { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }
}

public class BlogListResponse
{
    [JsonPropertyName("items")]
    public List<BlogDto> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Math.Max(PageSize, 1));
}

public class BlogCategoryOptionDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = null!;
}

public class CreateBlogRequest
{
    [JsonPropertyName("title")]
    [Required(ErrorMessage = "Title không được để trống.")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("slug")]
    [Required(ErrorMessage = "Slug không được để trống.")]
    public string Slug { get; set; } = null!;

    [JsonPropertyName("content")]
    [Required(ErrorMessage = "Content không được để trống.")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; } = 0; // 0=Draft
}

public class UpdateBlogRequest
{
    [JsonPropertyName("title")]
    [Required(ErrorMessage = "Title không được để trống.")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("slug")]
    [Required(ErrorMessage = "Slug không được để trống.")]
    public string Slug { get; set; } = null!;

    [JsonPropertyName("content")]
    [Required(ErrorMessage = "Content không được để trống.")]
    public string Content { get; set; } = null!;

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}
