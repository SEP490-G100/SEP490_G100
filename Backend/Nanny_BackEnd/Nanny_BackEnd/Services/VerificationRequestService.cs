using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class VerificationRequestService
{
    private readonly VerificationRequestRepository _repo;

    public VerificationRequestService(VerificationRequestRepository repo)
    {
        _repo = repo;
    }

    public async Task<VerificationRequestListResponse> GetListAsync(
        int? status,
        string? search,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 3;

        var (items, totalCount) = await _repo.GetListAsync(status, search, page, pageSize);

        var dtos = items.Select(v => new VerificationRequestListDto
        {
            Id             = v.Id,
            NannyProfileId = v.NannyProfileId,
            Status         = v.Status,
            CreatedAt      = v.CreatedAt,
            ReviewedAt     = v.ReviewedAt,
            NannyUserId    = v.NannyProfile.UserId,
            NannyFirstName = v.NannyProfile.User.FirstName,
            NannyLastName  = v.NannyProfile.User.LastName,
            NannyEmail     = v.NannyProfile.User.Email,
            NannyAvatarUrl = v.NannyProfile.User.AvatarUrl,
            NannyCity      = v.NannyProfile.User.City
        }).ToList();

        return new VerificationRequestListResponse
        {
            Items      = dtos,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<(bool Success, VerificationRequestDetailDto? Data, string? Message)> GetDetailAsync(Guid id)
    {
        var v = await _repo.GetByIdAsync(id);

        if (v == null)
            return (false, null, "Không tìm thấy yêu cầu xác minh.");

        var dto = new VerificationRequestDetailDto
        {
            Id                 = v.Id,
            NannyProfileId     = v.NannyProfileId,
            Status             = v.Status,
            RejectionReason    = v.RejectionReason,
            CreatedAt          = v.CreatedAt,
            ReviewedAt         = v.ReviewedAt,
            ReviewedBy         = v.ReviewedBy,

            NannyUserId        = v.NannyProfile.UserId,
            NannyFirstName     = v.NannyProfile.User.FirstName,
            NannyLastName      = v.NannyProfile.User.LastName,
            NannyEmail         = v.NannyProfile.User.Email,
            NannyPhoneNumber   = v.NannyProfile.User.PhoneNumber,
            NannyAvatarUrl     = v.NannyProfile.User.AvatarUrl,
            NannyCity          = v.NannyProfile.User.City,

            Bio                = v.NannyProfile.Bio,
            YearsOfExperience  = v.NannyProfile.YearsOfExperience,
            EducationLevel     = (int?) v.NannyProfile.EducationLevel,
            VerificationStatus = (int) v.NannyProfile.VerificationStatus,

            Documents = v.VerificationDocuments.Select(d => new VerificationDocumentDto
            {
                Id           = d.Id,
                DocumentType = d.DocumentType,
                DocumentUrl  = d.DocumentUrl,
                FileName     = d.FileName,
                FileSize     = d.FileSize
            }).ToList()
        };

        return (true, dto, null);
    }

    public async Task<(bool Success, int StatusCode, string Message)> ReviewAsync(Guid id, ReviewVerificationRequest request)
    {
        // Action: 2 = Approved, 3 = Rejected
        if (request.Action != (int)Enums.VerificationStatus.Approved && request.Action != (int)Enums.VerificationStatus.Rejected)
            return (false, 400, "Action không hợp lệ. Chỉ chấp nhận 2 (Approve) hoặc 3 (Reject).");

        if (request.Action == (int)Enums.VerificationStatus.Rejected && string.IsNullOrWhiteSpace(request.RejectionReason))
            return (false, 400, "Lý do từ chối (RejectionReason) là bắt buộc khi từ chối yêu cầu.");

        var v = await _repo.GetByIdAsync(id);

        if (v == null)
            return (false, 404, "Không tìm thấy yêu cầu xác minh.");

        if (v.Status != (int)Enums.VerificationStatus.Pending)
            return (false, 409, "Yêu cầu này đã được xử lý trước đó.");

        // Update VerificationRequest
        v.Status          = request.Action;     // 2 = Approved, 3 = Rejected
        v.ReviewedBy      = request.ReviewedBy;
        v.ReviewedAt      = DateTime.UtcNow;
        v.RejectionReason = request.Action == (int)Enums.VerificationStatus.Rejected ? request.RejectionReason?.Trim() : null;
        v.UpdatedAt       = DateTime.UtcNow;

        // Sync NannyProfile.VerificationStatus accordingly
        var nannyProfile = await _repo.GetNannyProfileAsync(v.NannyProfileId);
        if (nannyProfile != null)
        {
            nannyProfile.VerificationStatus = (Enums.VerificationStatus) request.Action; // 1=Approved, 2=Rejected
            nannyProfile.VerifiedAt = request.Action == (int)Enums.VerificationStatus.Approved ? DateTime.UtcNow : null;
            nannyProfile.VerifiedBy = request.Action == (int)Enums.VerificationStatus.Approved ? request.ReviewedBy : null;
            nannyProfile.UpdatedAt  = DateTime.UtcNow;
        }

        await _repo.SaveChangesAsync();

        var message = request.Action == (int)Enums.VerificationStatus.Approved ? "Đã duyệt yêu cầu xác minh." : "Đã từ chối yêu cầu xác minh.";
        return (true, 200, message);
    }
}
