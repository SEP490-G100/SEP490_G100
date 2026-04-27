using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class FaqRepository : IFaqRepository
{
    private readonly Sep490NannyDbContext _db;

    public FaqRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<(List<Faq> Items, int TotalCount)> GetPagedAsync(
        string? search,
        bool? isActive,
        string? category,
        int page,
        int pageSize)
    {
        var query = _db.Faqs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLower();
            query = query.Where(f =>
                f.Question.ToLower().Contains(normalized) ||
                f.Answer.ToLower().Contains(normalized) ||
                (f.Category != null && f.Category.ToLower().Contains(normalized)));
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

    public async Task<List<string>> GetDistinctCategoriesAsync() =>
        await _db.Faqs
            .Where(f => f.Category != null)
            .Select(f => f.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    public async Task<Faq?> GetByIdAsync(Guid id) =>
        await _db.Faqs.FirstOrDefaultAsync(f => f.Id == id);

    public void Add(Faq faq) => _db.Faqs.Add(faq);

    public async Task<int> GetMaxSortOrderAsync() =>
        await _db.Faqs
            .Select(f => (int?)f.SortOrder)
            .MaxAsync() ?? 0;

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
