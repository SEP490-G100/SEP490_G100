using Nanny_BackEnd.DTOs.BlogCategory;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class BlogCategoryService
{
    private readonly BlogCategoryRepository _repo;

    public BlogCategoryService(BlogCategoryRepository repo) => _repo = repo;

    // ── List (paged) ──────────────────────────────────────────────────────
    public async Task<BlogCategoryListResponse> GetCategoriesAsync(
        string? search, int page, int pageSize, bool? isDeleted = null)
    {
        var (items, total) = await _repo.GetPagedAsync(search, page, pageSize, isDeleted);

        var dtos = new List<BlogCategoryDto>();
        foreach (var c in items)
        {
            var count = await _repo.GetBlogCountAsync(c.Id);
            dtos.Add(MapToDto(c, count));
        }

        return new BlogCategoryListResponse
        {
            Items     = dtos,
            TotalCount = total,
            Page      = page,
            PageSize  = pageSize
        };
    }

    // ── Get single ────────────────────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message, BlogCategoryDto? Data)>
        GetCategoryAsync(Guid id)
    {
        var cat = await _repo.GetByIdAsync(id);
        if (cat == null) return (false, 404, "Không tìm thấy danh mục.", null);
        var count = await _repo.GetBlogCountAsync(id);
        return (true, 200, "OK", MapToDto(cat, count));
    }

    // ── Create ────────────────────────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message, BlogCategoryDto? Data)>
        CreateCategoryAsync(CreateBlogCategoryRequest req, Guid? createdBy)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return (false, 400, "Name không được để trống.", null);
        if (string.IsNullOrWhiteSpace(req.Slug))
            return (false, 400, "Slug không được để trống.", null);

        var slug = req.Slug.Trim().ToLower();
        if (await _repo.SlugExistsAsync(slug))
            return (false, 409, $"Slug '{slug}' đã tồn tại.", null);

        var maxOrder = await _repo.GetMaxSortOrderAsync();

        var cat = new BlogCategory
        {
            Id        = Guid.NewGuid(),
            Name      = req.Name.Trim(),
            Slug      = slug,
            SortOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsDeleted = false
        };

        _repo.Add(cat);
        await _repo.SaveChangesAsync();

        return (true, 201, "Tạo danh mục thành công.", MapToDto(cat, 0));
    }

    // ── Update ────────────────────────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message)>
        UpdateCategoryAsync(Guid id, UpdateBlogCategoryRequest req, Guid? updatedBy)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return (false, 400, "Name không được để trống.");
        if (string.IsNullOrWhiteSpace(req.Slug))
            return (false, 400, "Slug không được để trống.");

        var cat = await _repo.GetByIdAsync(id);
        if (cat == null) return (false, 404, "Không tìm thấy danh mục.");

        var slug = req.Slug.Trim().ToLower();
        if (await _repo.SlugExistsAsync(slug, excludeId: id))
            return (false, 409, $"Slug '{slug}' đã tồn tại.");

        cat.Name      = req.Name.Trim();
        cat.Slug      = slug;
        cat.UpdatedAt = DateTime.UtcNow;
        cat.UpdatedBy = updatedBy;

        await _repo.SaveChangesAsync();
        return (true, 200, "Cập nhật danh mục thành công.");
    }

    // ── Toggle Status ─────────────────────────────────────────────────────
    public async Task<(bool Success, int StatusCode, string Message)>
        ToggleCategoryStatusAsync(Guid id, bool activate)
    {
        var cat = await _repo.GetByIdAsync(id);
        if (cat == null) return (false, 404, "Không tìm thấy danh mục.");

        if (!activate)
        {
            var blogCount = await _repo.GetBlogCountAsync(id);
            if (blogCount > 0)
                return (false, 400, "This blog category has blog");
        }

        _repo.ToggleStatus(cat, activate);
        await _repo.SaveChangesAsync();

        return (true, 200, activate ? "Đã kích hoạt danh mục." : "Đã hủy kích hoạt danh mục.");
    }

    // ── Helper ────────────────────────────────────────────────────────────
    private static BlogCategoryDto MapToDto(BlogCategory c, int blogCount) => new()
    {
        Id        = c.Id,
        Name      = c.Name,
        Slug      = c.Slug,
        SortOrder = c.SortOrder,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        IsDeleted = c.IsDeleted,
        BlogCount = blogCount
    };
}
