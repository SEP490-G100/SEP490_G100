using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class BlogRepository : IBlogRepository
{
    private readonly Sep490NannyDbContext _db;
    public BlogRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<(List<Blog> Items, int TotalCount)> GetBlogListAsync(
        string? search, int page, int pageSize,
        int? status = null, bool? isDeleted = null, Guid? categoryId = null,
        string? sort = null)
    {
        var query = _db.Blogs.AsQueryable();

        if (isDeleted.HasValue)
            query = query.Where(b => b.IsDeleted == isDeleted.Value);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (categoryId.HasValue)
            query = query.Where(b => b.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b => b.Title.ToLower().Contains(s) || b.Slug.ToLower().Contains(s));
        }

        var total = await query.CountAsync();

        IOrderedQueryable<Blog> ordered = sort switch
        {
            "popular" => query.OrderByDescending(b => b.ViewCount),
            "oldest"  => query.OrderBy(b => b.CreatedAt),
            _         => query.OrderByDescending(b => b.CreatedAt)
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(b => b.AuthorUser)
            .Include(b => b.Category)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Blog?> GetByIdAsync(Guid id) =>
        await _db.Blogs
            .Include(b => b.AuthorUser)
            .Include(b => b.Category)
            .FirstOrDefaultAsync(b => b.Id == id);

    public async Task<Blog?> GetBySlugAsync(string slug) =>
        await _db.Blogs
            .Include(b => b.AuthorUser)
            .Include(b => b.Category)
            .FirstOrDefaultAsync(b => b.Slug == slug && b.Status == (int)Nanny_BackEnd.Enums.BlogStatus.Published && !b.IsDeleted);

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null) =>
        await _db.Blogs.AnyAsync(b =>
            b.Slug == slug.Trim() &&
            (excludeId == null || b.Id != excludeId));

    public async Task<List<BlogCategory>> GetActiveCategoriesAsync() =>
        await _db.BlogCategories
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

    public void Add(Blog blog) => _db.Blogs.Add(blog);

    public void ToggleStatus(Blog blog, bool activate) => blog.IsDeleted = !activate;

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
