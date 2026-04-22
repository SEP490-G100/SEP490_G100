using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Blog;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IBlogService
{
    Task<List<BlogCategoryOptionDto>> GetActiveCategoriesAsync();
    Task<BlogListResponse> ModeratorViewBlogListAsync(
        string? search, int page, int pageSize,
        int? status = null, bool? isDeleted = null, Guid? categoryId = null,
        string? sort = null);
    Task<(bool Success, int StatusCode, string Message, BlogDto? Data)> ModeratorViewBlogDetailAsync(Guid id);
    Task<(bool Success, int StatusCode, string Message, BlogDto? Data)> GetBlogBySlugAsync(string slug);
    Task<(bool Success, int StatusCode, string Message, BlogDto? Data)> ModeratorCreateBlogAsync(CreateBlogRequest req, Guid authorId);
    Task<(bool Success, int StatusCode, string Message)> ModeratorUpdateBlogAsync(Guid id, UpdateBlogRequest req, Guid? updatedBy);
    Task<(bool Success, int StatusCode, string Message)> ModeratorToggleBlogStatusAsync(Guid id, bool activate);
}
