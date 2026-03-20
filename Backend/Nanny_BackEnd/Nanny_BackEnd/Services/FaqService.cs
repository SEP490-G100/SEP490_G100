using Nanny_BackEnd.DTOs.FAQ;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

/// <summary>
/// Handles FAQ management operations (create, read, update, soft-delete).
/// Used by Moderator role.
/// </summary>
public class FaqService
{
    private readonly FaqRepository _faqRepo;

    public FaqService(FaqRepository faqRepo)
    {
        _faqRepo = faqRepo;
    }

    /// <summary>Paginated list of FAQ entries.</summary>
    public async Task<FaqListResponse> GetFaqsAsync(
        string? search, bool? isActive, string? category, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, totalCount) = await _faqRepo.GetPagedAsync(search, isActive, category, page, pageSize);

        var dtos = items.Select(f => new FaqDto
        {
            Id        = f.Id,
            Question  = f.Question,
            Answer    = f.Answer,
            Category  = f.Category,
            SortOrder = f.SortOrder,
            IsActive  = f.IsActive,
            ViewCount = f.ViewCount,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        }).ToList();

        return new FaqListResponse
        {
            Items      = dtos,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }

    /// <summary>Get distinct categories for filter dropdown.</summary>
    public async Task<List<string>> GetCategoriesAsync() =>
        await _faqRepo.GetDistinctCategoriesAsync();

    /// <summary>Get a single FAQ by Id.</summary>
    public async Task<(bool Success, FaqDto? Data, string? Message)> GetFaqAsync(Guid id)
    {
        var faq = await _faqRepo.GetByIdAsync(id);
        if (faq == null)
            return (false, null, "Không tìm thấy FAQ.");

        return (true, MapToDto(faq), null);
    }

    /// <summary>
    /// Create a new FAQ entry.
    /// SortOrder is auto-assigned as max(existing SortOrder) + 1.
    /// </summary>
    public async Task<(bool Success, int StatusCode, string Message, object? Data)> CreateFaqAsync(
        CreateFaqRequest request, Guid? createdBy)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return (false, 400, "Question không được để trống.", null);
        if (string.IsNullOrWhiteSpace(request.Answer))
            return (false, 400, "Answer không được để trống.", null);
        if (string.IsNullOrWhiteSpace(request.Category))
            return (false, 400, "Category không được để trống.", null);

        // Auto-assign SortOrder = max + 1
        var maxOrder = await _faqRepo.GetMaxSortOrderAsync();

        var faq = new Faq
        {
            Id        = Guid.NewGuid(),
            Question  = request.Question.Trim(),
            Answer    = request.Answer.Trim(),
            Category  = request.Category.Trim(),
            SortOrder = maxOrder + 1,
            IsActive  = request.IsActive,
            ViewCount = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            IsDeleted = false
        };

        _faqRepo.Add(faq);
        await _faqRepo.SaveChangesAsync();

        return (true, 201, "Tạo FAQ thành công.", new { faq.Id, faq.SortOrder });
    }

    /// <summary>
    /// Update question, answer, isActive of an existing FAQ.
    /// SortOrder is NOT updated (read-only after creation).
    /// </summary>
    public async Task<(bool Success, int StatusCode, string Message)> UpdateFaqAsync(
        Guid id, UpdateFaqRequest request, Guid? updatedBy)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return (false, 400, "Question không được để trống.");
        if (string.IsNullOrWhiteSpace(request.Answer))
            return (false, 400, "Answer không được để trống.");

        var faq = await _faqRepo.GetByIdAsync(id);
        if (faq == null)
            return (false, 404, "Không tìm thấy FAQ.");

        faq.Question  = request.Question.Trim();
        faq.Answer    = request.Answer.Trim();
        faq.IsActive  = request.IsActive;
        // SortOrder intentionally NOT updated
        faq.UpdatedAt = DateTime.UtcNow;
        faq.UpdatedBy = updatedBy;

        await _faqRepo.SaveChangesAsync();
        return (true, 200, "Cập nhật FAQ thành công.");
    }

    /// <summary>Soft-delete: sets IsDeleted = true, record stays in database.</summary>
    public async Task<(bool Success, int StatusCode, string Message)> DeleteFaqAsync(Guid id)
    {
        var faq = await _faqRepo.GetByIdAsync(id);
        if (faq == null)
            return (false, 404, "Không tìm thấy FAQ.");

        _faqRepo.SoftDelete(faq);
        await _faqRepo.SaveChangesAsync();
        return (true, 200, "Đã xóa FAQ.");
    }

    /// <summary>
    /// Toggle FAQ status:
    ///   Activate   → IsActive = true,  IsDeleted = false  (isActive:1, isDeleted:0)
    ///   Deactivate → IsActive = false, IsDeleted = true   (isActive:0, isDeleted:1)
    /// </summary>
    public async Task<(bool Success, int StatusCode, string Message, bool IsActive)> ToggleFaqStatusAsync(
        Guid id, Guid? updatedBy)
    {
        // Use GetByIdAsync which also finds soft-deleted (for re-activation)
        var faq = await _faqRepo.GetByIdAsync(id);
        if (faq == null)
            return (false, 404, "Không tìm thấy FAQ.", false);

        var willActivate = !faq.IsActive;

        if (willActivate)
        {
            // Activate: IsActive 0→1, IsDeleted 1→0
            faq.IsActive  = true;
            faq.IsDeleted = false;
        }
        else
        {
            // Deactivate: IsActive 1→0, IsDeleted 0→1
            faq.IsActive  = false;
            faq.IsDeleted = true;
        }

        faq.UpdatedAt = DateTime.UtcNow;
        faq.UpdatedBy = updatedBy;

        await _faqRepo.SaveChangesAsync();

        var msg = willActivate ? "FAQ đã được kích hoạt." : "FAQ đã bị vô hiệu hóa.";
        return (true, 200, msg, faq.IsActive);
    }

    private static FaqDto MapToDto(Faq f) => new()
    {
        Id        = f.Id,
        Question  = f.Question,
        Answer    = f.Answer,
        Category  = f.Category,
        SortOrder = f.SortOrder,
        IsActive  = f.IsActive,
        ViewCount = f.ViewCount,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };
}
