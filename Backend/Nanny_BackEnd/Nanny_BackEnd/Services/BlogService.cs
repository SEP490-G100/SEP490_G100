using Nanny_BackEnd.DTOs.Blog;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Enums;

namespace Nanny_BackEnd.Services;

public class BlogService
{
    private readonly BlogRepository _repo;
    public BlogService(BlogRepository repo) => _repo = repo;


    public async Task<BlogListResponse> ModeratorViewBlogListAsync(
        string? search, int page, int pageSize,
        int? status = null, bool? isDeleted = null, Guid? categoryId = null,
        string? sort = null)
    {
        var (items, total) = await _repo.GetBlogListAsync(search, page, pageSize, status, isDeleted, categoryId, sort);

        var dtos = items.Select(MapToDto).ToList();

        return new BlogListResponse
        {
            Items      = dtos,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    // ── Get single (by ID) ───────────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message, BlogDto? Data)>
        ModeratorViewBlogDetailAsync(Guid id)
    {
        var blog = await _repo.GetByIdAsync(id);
        if (blog == null) return (false, 404, "Không tìm thấy bài viết.", null);
        return (true, 200, "OK", MapToDto(blog));
    }

    // ── Get single (by Slug, public — increments ViewCount) ───────────────
    public async Task<(bool Success, int StatusCode, string Message, BlogDto? Data)>
        GetBlogBySlugAsync(string slug)
    {
        var blog = await _repo.GetBySlugAsync(slug);
        if (blog == null) return (false, 404, "Không tìm thấy bài viết.", null);

        blog.ViewCount++;
        await _repo.SaveChangesAsync();

        return (true, 200, "OK", MapToDto(blog));
    }

    // ── Create ────────────────────────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message, BlogDto? Data)>
        ModeratorCreateBlogAsync(CreateBlogRequest req, Guid authorId)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return (false, 400, "Title không được để trống.", null);
        if (string.IsNullOrWhiteSpace(req.Slug))
            return (false, 400, "Slug không được để trống.", null);
        if (string.IsNullOrWhiteSpace(req.Content))
            return (false, 400, "Content không được để trống.", null);

        var slug = req.Slug.Trim().ToLower();
        if (await _repo.SlugExistsAsync(slug))
            return (false, 409, $"Slug '{slug}' đã tồn tại.", null);

        var blog = new Blog
        {
            Id           = Guid.NewGuid(),
            Title        = req.Title.Trim(),
            Slug         = slug,
            Summary      = req.Summary?.Trim(),
            Content      = req.Content.Trim(),
            ThumbnailUrl = req.ThumbnailUrl?.Trim(),
            AuthorUserId = authorId,
            CategoryId   = req.CategoryId,
            Status       = req.Status,
            ViewCount    = 0,
            PublishedAt  = req.Status == (int)BlogStatus.Published ? DateTime.UtcNow : null,
            CreatedAt    = DateTime.UtcNow,
            CreatedBy    = authorId,
            IsDeleted    = false
        };

        _repo.Add(blog);
        await _repo.SaveChangesAsync();

        return (true, 201, "Tạo bài viết thành công.", MapToDto(blog));
    }

    // ── Update ────────────────────────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message)>
        ModeratorUpdateBlogAsync(Guid id, UpdateBlogRequest req, Guid? updatedBy)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return (false, 400, "Title không được để trống.");
        if (string.IsNullOrWhiteSpace(req.Slug))
            return (false, 400, "Slug không được để trống.");
        if (string.IsNullOrWhiteSpace(req.Content))
            return (false, 400, "Content không được để trống.");

        var blog = await _repo.GetByIdAsync(id);
        if (blog == null) return (false, 404, "Không tìm thấy bài viết.");

        var slug = req.Slug.Trim().ToLower();
        if (await _repo.SlugExistsAsync(slug, excludeId: id))
            return (false, 409, $"Slug '{slug}' đã tồn tại.");

        // Set publishedAt when first published
        if (blog.Status != (int)BlogStatus.Published && req.Status == (int)BlogStatus.Published && blog.PublishedAt == null)
            blog.PublishedAt = DateTime.UtcNow;

        blog.Title        = req.Title.Trim();
        blog.Slug         = slug;
        blog.Summary      = req.Summary?.Trim();
        blog.Content      = req.Content.Trim();
        blog.ThumbnailUrl = req.ThumbnailUrl?.Trim();
        blog.CategoryId   = req.CategoryId;
        blog.Status       = req.Status;
        blog.UpdatedAt    = DateTime.UtcNow;
        blog.UpdatedBy    = updatedBy;

        await _repo.SaveChangesAsync();
        return (true, 200, "Cập nhật bài viết thành công.");
    }

    // ── Toggle Status (IsDeleted) ─────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message)>
        ModeratorToggleBlogStatusAsync(Guid id, bool activate)
    {
        var blog = await _repo.GetByIdAsync(id);
        if (blog == null) return (false, 404, "Không tìm thấy bài viết.");

        _repo.ToggleStatus(blog, activate);
        await _repo.SaveChangesAsync();

        return (true, 200, activate ? "Đã kích hoạt bài viết." : "Đã hủy kích hoạt bài viết.");
    }

    // ── Helper ────────────────────────────────────────────────────────────
    private static BlogDto MapToDto(Blog b) => new()
    {
        Id           = b.Id,
        Title        = b.Title,
        Slug         = b.Slug,
        Summary      = b.Summary,
        Content      = b.Content,
        ThumbnailUrl = b.ThumbnailUrl,
        AuthorUserId = b.AuthorUserId,
        AuthorName   = b.AuthorUser != null ? $"{b.AuthorUser.FirstName} {b.AuthorUser.LastName}" : null,
        CategoryId   = b.CategoryId,
        CategoryName = b.Category?.Name,
        Status       = b.Status,
        ViewCount    = b.ViewCount,
        PublishedAt  = b.PublishedAt,
        CreatedAt    = b.CreatedAt,
        UpdatedAt    = b.UpdatedAt,
        IsDeleted    = b.IsDeleted
    };
}
