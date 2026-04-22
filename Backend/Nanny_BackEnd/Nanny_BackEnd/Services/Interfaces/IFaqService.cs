using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.FAQ;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IFaqService
{
    Task<FaqListResponse> ModeratorViewFaqListAsync(
        string? search,
        bool? isActive,
        string? category,
        int page,
        int pageSize);
    Task<List<string>> ModeratorViewFaqCategoriesAsync();
    Task<(bool Success, FaqDto? Data, string? Message)> ModeratorViewFaqDetailAsync(Guid id);
    Task<(bool Success, int StatusCode, string Message, object? Data)> ModeratorCreateFaqAsync(
        CreateFaqRequest request,
        Guid? createdBy);
    Task<(bool Success, int StatusCode, string Message)> ModeratorUpdateFaqAsync(
        Guid id,
        UpdateFaqRequest request,
        Guid? updatedBy);
    Task<(bool Success, int StatusCode, string Message, bool IsActive)> ModeratorToggleFaqStatusAsync(
        Guid id,
        Guid? updatedBy);
}
