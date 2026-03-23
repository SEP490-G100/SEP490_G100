using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class VerificationRequestService
{
    private readonly VerificationRequestRepository _repo;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

    public VerificationRequestService(VerificationRequestRepository repo, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _repo = repo;
        _env = env;
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
            NannyAddress       = v.NannyProfile.User.Address,
            NannyDistrict      = v.NannyProfile.User.District,
            NannyWard          = v.NannyProfile.User.Ward,
            NannyGender        = v.NannyProfile.User.Gender,
            NannyDateOfBirth   = v.NannyProfile.User.DateOfBirth,

            Bio                = v.NannyProfile.Bio,
            YearsOfExperience  = v.NannyProfile.YearsOfExperience,
            EducationLevel     = (int?) v.NannyProfile.EducationLevel,
            VerificationStatus = (int) v.NannyProfile.VerificationStatus,
            ExpectedSalaryMin  = v.NannyProfile.ExpectedSalaryMin,
            ExpectedSalaryMax  = v.NannyProfile.ExpectedSalaryMax,
            SalaryType         = v.NannyProfile.SalaryType,
            MaxTravelDistance  = v.NannyProfile.MaxTravelDistance,

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
        if (request.Action != (int)Enums.NannyVerificationRequestStatus.Approved && request.Action != (int)Enums.NannyVerificationRequestStatus.Rejected)
            return (false, 400, "Action không hợp lệ. Chỉ chấp nhận 2 (Approve) hoặc 3 (Reject).");

        if (request.Action == (int)Enums.NannyVerificationRequestStatus.Rejected && string.IsNullOrWhiteSpace(request.RejectionReason))
            return (false, 400, "Lý do từ chối (RejectionReason) là bắt buộc khi từ chối yêu cầu.");

        var v = await _repo.GetByIdAsync(id);

        if (v == null)
            return (false, 404, "Không tìm thấy yêu cầu xác minh.");

        if (v.Status != (int)Enums.NannyVerificationRequestStatus.Pending)
            return (false, 409, "Yêu cầu này đã được xử lý trước đó.");

        // Update VerificationRequest
        v.Status          = request.Action;     // 2 = Approved, 3 = Rejected
        v.ReviewedBy      = request.ReviewedBy;
        v.ReviewedAt      = DateTime.UtcNow;
        v.RejectionReason = request.Action == (int)Enums.NannyVerificationRequestStatus.Rejected ? request.RejectionReason?.Trim() : null;
        v.UpdatedAt       = DateTime.UtcNow;

        // Sync NannyProfile.VerificationStatus accordingly
        var nannyProfile = await _repo.GetNannyProfileAsync(v.NannyProfileId);
        if (nannyProfile != null)
        {
            nannyProfile.VerificationStatus = (int)(Enums.NannyVerificationRequestStatus)request.Action; // 1=Approved, 2=Rejected
            nannyProfile.VerifiedAt = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved ? DateTime.UtcNow : null;
            nannyProfile.VerifiedBy = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved ? request.ReviewedBy : null;
            nannyProfile.UpdatedAt  = DateTime.UtcNow;
        }

        await _repo.SaveChangesAsync();

        var message = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved ? "Đã duyệt yêu cầu xác minh." : "Đã từ chối yêu cầu xác minh.";
        return (true, 200, message);
    }

    public async Task<List<VerificationRequestListDto>> GetNannyRequestsAsync(Guid userId)
    {
        var profile = await _repo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null) return new List<VerificationRequestListDto>();

        var requests = await _repo.GetRequestsByNannyProfileAsync(profile.Id);
        
        return requests.Select(v => new VerificationRequestListDto
        {
            Id = v.Id,
            NannyProfileId = v.NannyProfileId,
            Status = v.Status,
            CreatedAt = v.CreatedAt,
            ReviewedAt = v.ReviewedAt
            // Ignoring deep NannyProfile/User mappings here since Nanny only sees their own requests
        }).ToList();
    }

    public async Task<(bool Success, string Message)> SubmitRequestAsync(Guid userId, SubmitVerificationRequestDto request)
    {
        var profile = await _repo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
            return (false, "Không tìm thấy hồ sơ Nanny.");

        // Check if there's already a pending request
        var existingRequests = await _repo.GetRequestsByNannyProfileAsync(profile.Id);
        if (existingRequests.Any(r => r.Status == (int)Enums.NannyVerificationRequestStatus.Pending))
        {
            return (false, "Bạn đã có một yêu cầu xác minh đang chờ duyệt.");
        }

        var verificationReq = new Nanny_BackEnd.Models.VerificationRequest
        {
            Id = Guid.NewGuid(),
            NannyProfileId = profile.Id,
            Status = (int)Enums.NannyVerificationRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        if (request.Documents == null || !request.Documents.Any())
        {
            return (false, "Bạn phải tải lên ít nhất một tài liệu.");
        }

        foreach (var doc in request.Documents)
        {
            verificationReq.VerificationDocuments.Add(new Nanny_BackEnd.Models.VerificationDocument
            {
                Id = Guid.NewGuid(),
                VerificationRequestId = verificationReq.Id,
                DocumentType = 1, // DocumentType is 1 as requested
                DocumentUrl = doc.DocumentUrl,
                FileName = doc.FileName,
                FileSize = doc.FileSize,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        _repo.AddRequest(verificationReq);
        await _repo.SaveChangesAsync();

        return (true, "Gửi yêu cầu xác minh thành công.");
    }
}
