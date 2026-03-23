using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class FaqRepository
{
    private readonly Sep490NannyDbContext _db;

    public FaqRepository(Sep490NannyDbContext db) => _db = db;

    /// <summary>
    /// Paginated list for MODERATOR view — includes ALL FAQs (active and inactive/soft-deleted).
    /// isActive filter: true=Active (isActive=1,isDeleted=0), false=Inactive (isActive=0,isDeleted=1).
    /// </summary>
    public async Task<(List<Faq> Items, int TotalCount)> GetPagedAsync(
        string? search, bool? isActive, string? category, int page, int pageSize)
    {
        // Moderator sees all records — no IsDeleted filter here
        var query = _db.Faqs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(f =>
                f.Question.ToLower().Contains(s) ||
                f.Answer.ToLower().Contains(s) ||
                (f.Category != null && f.Category.ToLower().Contains(s)));
        }

        if (isActive.HasValue)
            query = query.Where(f => f.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(f => f.Category != null && f.Category == category.Trim());

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(f => f.SortOrder)
            .ThenByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>Get all distinct categories (from all records, including inactive).</summary>
    public async Task<List<string>> GetDistinctCategoriesAsync() =>
        await _db.Faqs
            .Where(f => f.Category != null)
            .Select(f => f.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    /// <summary>
    /// Get a single FAQ by Id — includes soft-deleted records so moderators
    /// can reactivate them (toggle Inactive → Active).
    /// </summary>
    public async Task<Faq?> GetByIdAsync(Guid id) =>
        await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id);

    /// <summary>Get a single FAQ by Id — only non-deleted (for public-facing read).</summary>
    public async Task<Faq?> GetByIdPublicAsync(Guid id) =>
        await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted && f.IsActive);

    /// <summary>Add a new FAQ.</summary>
    public void Add(Faq faq) => _db.Faqs.Add(faq);

    /// <summary>Get the current maximum SortOrder value (all records).</summary>
    public async Task<int> GetMaxSortOrderAsync() =>
        await _db.Faqs
            .Select(f => (int?)f.SortOrder)
            .MaxAsync() ?? 0;

    /// <summary>
    /// Soft-delete: IsDeleted = true, IsActive = false.
    /// Called only on permanent/explicit delete, NOT on toggle deactivate.
    /// </summary>
    public void SoftDelete(Faq faq)
    {
        faq.IsDeleted = true;
        faq.IsActive  = false;
    }

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
