using Nanny_BackEnd.DTOs.FAQ;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class FaqService : IFaqService
{
    private readonly IFaqRepository _faqRepository;

    public FaqService(IFaqRepository faqRepository)
    {
        _faqRepository = faqRepository;
    }

    public async Task<FaqListResponse> ModeratorViewFaqListAsync(
        string? search,
        bool? isActive,
        string? category,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, totalCount) = await _faqRepository.GetPagedAsync(search, isActive, category, page, pageSize);

        var dtos = items.Select(f => new FaqDto
        {
            Id = f.Id,
            Question = f.Question,
            Answer = f.Answer,
            Category = f.Category,
            SortOrder = f.SortOrder,
            IsActive = f.IsActive,
            ViewCount = f.ViewCount,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        }).ToList();

        return new FaqListResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<string>> ModeratorViewFaqCategoriesAsync() =>
        await _faqRepository.GetDistinctCategoriesAsync();

    public async Task<(bool Success, FaqDto? Data, string? Message)> ModeratorViewFaqDetailAsync(Guid id)
    {
        var faq = await _faqRepository.GetByIdAsync(id);
        if (faq == null)
            return (false, null, "Khong tim thay FAQ.");

        return (true, mapToDto(faq), null);
    }

    public async Task<(bool Success, int StatusCode, string Message, object? Data)> ModeratorCreateFaqAsync(
        CreateFaqRequest request,
        Guid? createdBy)
    {
        if (request == null)
            return (false, 400, "Request body is required.", null);
        if (string.IsNullOrWhiteSpace(request.Question))
            return (false, 400, "Question khong duoc de trong.", null);
        if (string.IsNullOrWhiteSpace(request.Answer))
            return (false, 400, "Answer khong duoc de trong.", null);
        if (string.IsNullOrWhiteSpace(request.Category))
            return (false, 400, "Category khong duoc de trong.", null);

        var maxOrder = await _faqRepository.GetMaxSortOrderAsync();

        var faq = new Faq
        {
            Id = Guid.NewGuid(),
            Question = request.Question.Trim(),
            Answer = request.Answer.Trim(),
            Category = request.Category.Trim(),
            SortOrder = maxOrder + 1,
            IsActive = request.IsActive,
            ViewCount = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsDeleted = false
        };

        _faqRepository.Add(faq);
        await _faqRepository.SaveChangesAsync();

        return (true, 201, "Tao FAQ thanh cong.", new { faq.Id, faq.SortOrder });
    }

    public async Task<(bool Success, int StatusCode, string Message)> ModeratorUpdateFaqAsync(
        Guid id,
        UpdateFaqRequest request,
        Guid? updatedBy)
    {
        if (request == null)
            return (false, 400, "Request body is required.");
        if (string.IsNullOrWhiteSpace(request.Question))
            return (false, 400, "Question khong duoc de trong.");
        if (string.IsNullOrWhiteSpace(request.Answer))
            return (false, 400, "Answer khong duoc de trong.");

        var faq = await _faqRepository.GetByIdAsync(id);
        if (faq == null)
            return (false, 404, "Khong tim thay FAQ.");

        faq.Question = request.Question.Trim();
        faq.Answer = request.Answer.Trim();

        // Keep IsActive/IsDeleted consistent: if either indicates deactivation, mark as deleted/inactive.
        var shouldDeactivate = request.IsDeleted || !request.IsActive;
        faq.IsActive = !shouldDeactivate;
        faq.IsDeleted = shouldDeactivate;
        faq.UpdatedAt = DateTime.UtcNow;
        faq.UpdatedBy = updatedBy;

        await _faqRepository.SaveChangesAsync();
        return (true, 200, "Cap nhat FAQ thanh cong.");
    }

    public async Task<(bool Success, int StatusCode, string Message, bool IsActive)> ModeratorToggleFaqStatusAsync(
        Guid id,
        Guid? updatedBy)
    {
        var faq = await _faqRepository.GetByIdAsync(id);
        if (faq == null)
            return (false, 404, "Khong tim thay FAQ.", false);

        var willActivate = !faq.IsActive;
        if (willActivate)
        {
            faq.IsActive = true;
            faq.IsDeleted = false;
        }
        else
        {
            faq.IsActive = false;
            faq.IsDeleted = true;
        }

        faq.UpdatedAt = DateTime.UtcNow;
        faq.UpdatedBy = updatedBy;

        await _faqRepository.SaveChangesAsync();

        var message = willActivate ? "FAQ da duoc kich hoat." : "FAQ da bi vo hieu hoa.";
        return (true, 200, message, faq.IsActive);
    }

    private static FaqDto mapToDto(Faq faq) => new()
    {
        Id = faq.Id,
        Question = faq.Question,
        Answer = faq.Answer,
        Category = faq.Category,
        SortOrder = faq.SortOrder,
        IsActive = faq.IsActive,
        ViewCount = faq.ViewCount,
        CreatedAt = faq.CreatedAt,
        UpdatedAt = faq.UpdatedAt
    };
}
