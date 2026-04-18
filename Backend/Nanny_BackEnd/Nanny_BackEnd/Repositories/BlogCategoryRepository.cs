using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class BlogCategoryRepository
{
    private readonly Sep490NannyDbContext _db;
    public BlogCategoryRepository(Sep490NannyDbContext db) => _db = db;

    /// <summary>Paginated list with optional isDeleted filter.</summary>
    public async Task<(List<BlogCategory> Items, int TotalCount)> GetBlogCategoryListAsync(
        string? search, int page, int pageSize, bool? isDeleted = null)
    {
        // If filter given, apply it. Otherwise show all.
        var query = _db.BlogCategories.AsQueryable();
        if (isDeleted.HasValue)
            query = query.Where(c => c.IsDeleted == isDeleted.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || c.Slug.ToLower().Contains(s));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<BlogCategory?> GetByIdAsync(Guid id) =>
        await _db.BlogCategories.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null) =>
        await _db.BlogCategories.AnyAsync(c =>
            c.Slug == slug.Trim() &&
            (excludeId == null || c.Id != excludeId));

    public async Task<int> GetMaxSortOrderAsync() =>
        await _db.BlogCategories
            .Select(c => (int?)c.SortOrder)
            .MaxAsync() ?? 0;

    /// <summary>Count blogs belonging to a category (for display).</summary>
    public async Task<int> GetBlogCountAsync(Guid categoryId) =>
        await _db.Blogs.CountAsync(b => b.CategoryId == categoryId && !b.IsDeleted);

    public void Add(BlogCategory cat) => _db.BlogCategories.Add(cat);

    public void ToggleStatus(BlogCategory cat, bool activate) => cat.IsDeleted = !activate;

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
