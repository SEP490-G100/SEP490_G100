using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IFaqRepository
{
    Task<(List<Faq> Items, int TotalCount)> GetPagedAsync(
        string? search,
        bool? isActive,
        string? category,
        int page,
        int pageSize);
    Task<List<string>> GetDistinctCategoriesAsync();
    Task<Faq?> GetByIdAsync(Guid id);
    void Add(Faq faq);
    Task<int> GetMaxSortOrderAsync();
    Task SaveChangesAsync();
}
