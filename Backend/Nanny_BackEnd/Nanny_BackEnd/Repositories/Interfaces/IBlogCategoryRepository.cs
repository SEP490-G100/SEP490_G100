using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IBlogCategoryRepository
{
    Task<(List<BlogCategory> Items, int TotalCount)> GetBlogCategoryListAsync(
        string? search, int page, int pageSize, bool? isDeleted = null);
    Task<BlogCategory?> GetByIdAsync(Guid id);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null);
    Task<int> GetMaxSortOrderAsync();
    Task<int> GetBlogCountAsync(Guid categoryId);
    void Add(BlogCategory cat);
    void ToggleStatus(BlogCategory cat, bool activate);
    Task SaveChangesAsync();
}
