using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IBlogRepository
{
    Task<(List<Blog> Items, int TotalCount)> GetBlogListAsync(
        string? search, int page, int pageSize,
        int? status = null, bool? isDeleted = null, Guid? categoryId = null,
        string? sort = null);
    Task<Blog?> GetByIdAsync(Guid id);
    Task<Blog?> GetBySlugAsync(string slug);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null);
    Task<List<BlogCategory>> GetActiveCategoriesAsync();
    void Add(Blog blog);
    void ToggleStatus(Blog blog, bool activate);
    Task SaveChangesAsync();
}
