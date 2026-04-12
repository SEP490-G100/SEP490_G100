using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class VerificationRequestService
{
    private readonly VerificationRequestRepository _repo;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly NotificationService _notificationService;

    public VerificationRequestService(
        VerificationRequestRepository repo,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        NotificationService notificationService)
    {
        _repo = repo;
        _env = env;
        _notificationService = notificationService;
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

        return new VerificationRequestListResponse
        {
            Items = items.Select(MapListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, VerificationRequestDetailDto? Data, string? Message)> GetDetailAsync(Guid id)
    {
        var request = await _repo.GetByIdAsync(id);

        if (request == null)
        {
            return (false, null, "Khong tim thay yeu cau xac minh.");
        }

        return (true, MapDetailDto(request), null);
    }

    public async Task<(bool Success, int StatusCode, string Message)> ReviewAsync(Guid id, Guid moderatorId, ReviewVerificationRequest request)
    {
        if (request.Action != (int)Enums.NannyVerificationRequestStatus.Approved &&
            request.Action != (int)Enums.NannyVerificationRequestStatus.Rejected)
        {
            return (false, 400, "Action khong hop le. Chi chap nhan 2 (Approve) hoac 3 (Reject).");
        }

        if (request.Action == (int)Enums.NannyVerificationRequestStatus.Rejected &&
            string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return (false, 400, "Ly do tu choi la bat buoc khi tu choi yeu cau.");
        }

        var verificationRequest = await _repo.GetByIdAsync(id);

        if (verificationRequest == null)
        {
            return (false, 404, "Khong tim thay yeu cau xac minh.");
        }

        if (verificationRequest.Status != (int)Enums.NannyVerificationRequestStatus.Pending)
        {
            return (false, 409, "Yeu cau nay da duoc xu ly truoc do.");
        }

        verificationRequest.Status = request.Action;
        verificationRequest.ReviewedBy = moderatorId;
        verificationRequest.ReviewedAt = DateTime.UtcNow;
        verificationRequest.RejectionReason = request.Action == (int)Enums.NannyVerificationRequestStatus.Rejected
            ? request.RejectionReason?.Trim()
            : null;
        verificationRequest.UpdatedAt = DateTime.UtcNow;
        verificationRequest.UpdatedBy = moderatorId;

        var nannyProfile = await _repo.GetNannyProfileAsync(verificationRequest.NannyProfileId);
        if (nannyProfile != null)
        {
            nannyProfile.VerificationStatus = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved
                ? (int)VerificationStatus.Approved
                : (int)VerificationStatus.Rejected;
            nannyProfile.VerifiedAt = DateTime.UtcNow;
            nannyProfile.VerifiedBy = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved
                ? moderatorId
                : null;
            nannyProfile.UpdatedAt = DateTime.UtcNow;
            nannyProfile.UpdatedBy = moderatorId;
        }

        await _repo.SaveChangesAsync();

        var notificationTitle = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved
            ? "Yeu cau xac minh cua ban da duoc chap thuan"
            : "Yeu cau xac minh cua ban da bi tu choi";
        var notificationContent = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved
            ? "Moderator da chap thuan yeu cau xac minh cua ban."
            : $"Moderator da tu choi yeu cau xac minh cua ban. {(string.IsNullOrWhiteSpace(verificationRequest.RejectionReason) ? string.Empty : $"Ly do: {verificationRequest.RejectionReason}")}".Trim();
        var notificationType = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved
            ? NotificationTypes.VerificationRequestApproved
            : NotificationTypes.VerificationRequestRejected;

        await _notificationService.createNotification(
            verificationRequest.NannyProfile.UserId,
            notificationTitle,
            notificationContent,
            notificationType,
            verificationRequest.Id,
            "VerificationRequest",
            moderatorId);

        var message = request.Action == (int)Enums.NannyVerificationRequestStatus.Approved
            ? "Da duyet yeu cau xac minh."
            : "Da tu choi yeu cau xac minh.";

        return (true, 200, message);
    }

    public async Task<VerificationRequestListResponse> NannyGetVerificationRequestsAsync(Guid userId, int? status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 3;

        var profile = await _repo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
        {
            return new VerificationRequestListResponse
            {
                Items = new List<VerificationRequestListDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }

        var requests = await _repo.GetRequestsByNannyProfileAsync(profile.Id);
        var filteredRequests = status.HasValue
            ? requests.Where(request => request.Status == status.Value)
            : requests.AsEnumerable();

        var totalCount = filteredRequests.Count();
        var pageItems = filteredRequests
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapListDto)
            .ToList();

        return new VerificationRequestListResponse
        {
            Items = pageItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, VerificationRequestDetailDto? Data, string? Message)> NannyGetVerificationRequestDetailAsync(Guid userId, Guid id)
    {
        var profile = await _repo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
        {
            return (false, null, "Khong tim thay ho so Nanny.");
        }

        var request = await _repo.GetByIdAsync(id);
        if (request == null || request.NannyProfileId != profile.Id)
        {
            return (false, null, "Khong tim thay yeu cau xac minh.");
        }

        return (true, MapDetailDto(request), null);
    }

    public async Task<(bool Success, string Message)> NannySubmitVerificationRequestAsync(Guid userId, SubmitVerificationRequestDto request)
    {
        var profile = await _repo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
        {
            return (false, "Khong tim thay ho so Nanny.");
        }

        var existingRequests = await _repo.GetRequestsByNannyProfileAsync(profile.Id);
        if (existingRequests.Any(existingRequest => existingRequest.Status == (int)Enums.NannyVerificationRequestStatus.Pending))
        {
            return (false, "Ban da co mot yeu cau xac minh dang cho duyet.");
        }

        if (request.Documents == null || !request.Documents.Any())
        {
            return (false, "Ban phai tai len it nhat mot tai lieu.");
        }

        var verificationRequest = new Nanny_BackEnd.Models.VerificationRequest
        {
            Id = Guid.NewGuid(),
            NannyProfileId = profile.Id,
            Status = (int)Enums.NannyVerificationRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        foreach (var document in request.Documents)
        {
            var documentType = Enum.IsDefined(typeof(VerificationDocumentType), document.DocumentType)
                ? document.DocumentType
                : (int)VerificationDocumentType.IdentityCard;

            verificationRequest.VerificationDocuments.Add(new Nanny_BackEnd.Models.VerificationDocument
            {
                Id = Guid.NewGuid(),
                VerificationRequestId = verificationRequest.Id,
                DocumentType = documentType,
                DocumentUrl = document.DocumentUrl,
                FileName = document.FileName,
                FileSize = document.FileSize,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        _repo.AddRequest(verificationRequest);

        profile.VerificationStatus = (int)VerificationStatus.Pending;
        profile.VerifiedAt = DateTime.UtcNow;
        profile.VerifiedBy = null;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = userId;

        await _repo.SaveChangesAsync();

        await _notificationService.createNotificationForModerators(
            "Co yeu cau xac minh moi",
            "Mot nanny vua gui yeu cau xac minh moi va dang cho moderator xem xet.",
            NotificationTypes.VerificationRequestSubmitted,
            verificationRequest.Id,
            "VerificationRequest",
            userId);

        return (true, "Gui yeu cau xac minh thanh cong.");
    }

    private static VerificationRequestListDto MapListDto(Nanny_BackEnd.Models.VerificationRequest request)
    {
        return new VerificationRequestListDto
        {
            Id = request.Id,
            NannyProfileId = request.NannyProfileId,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            ReviewedAt = request.ReviewedAt,
            ReviewedBy = request.ReviewedBy,
            ReviewedByName = request.ReviewedByNavigation == null
                ? null
                : $"{request.ReviewedByNavigation.FirstName} {request.ReviewedByNavigation.LastName}".Trim(),
            RejectionReason = request.RejectionReason,
            NannyUserId = request.NannyProfile.UserId,
            NannyFirstName = request.NannyProfile.User.FirstName,
            NannyLastName = request.NannyProfile.User.LastName,
            NannyEmail = request.NannyProfile.User.Email,
            NannyAvatarUrl = request.NannyProfile.User.AvatarUrl,
            NannyCity = request.NannyProfile.User.City
        };
    }

    private static VerificationRequestDetailDto MapDetailDto(Nanny_BackEnd.Models.VerificationRequest request)
    {
        return new VerificationRequestDetailDto
        {
            Id = request.Id,
            NannyProfileId = request.NannyProfileId,
            Status = request.Status,
            RejectionReason = request.RejectionReason,
            CreatedAt = request.CreatedAt,
            ReviewedAt = request.ReviewedAt,
            ReviewedBy = request.ReviewedBy,
            ReviewedByName = request.ReviewedByNavigation == null
                ? null
                : $"{request.ReviewedByNavigation.FirstName} {request.ReviewedByNavigation.LastName}".Trim(),
            NannyUserId = request.NannyProfile.UserId,
            NannyFirstName = request.NannyProfile.User.FirstName,
            NannyLastName = request.NannyProfile.User.LastName,
            NannyEmail = request.NannyProfile.User.Email,
            NannyPhoneNumber = request.NannyProfile.User.PhoneNumber,
            NannyAvatarUrl = request.NannyProfile.User.AvatarUrl,
            NannyCity = request.NannyProfile.User.City,
            NannyAddress = request.NannyProfile.User.Address,
            NannyDistrict = request.NannyProfile.User.District,
            NannyWard = request.NannyProfile.User.Ward,
            NannyGender = request.NannyProfile.User.Gender,
            NannyDateOfBirth = request.NannyProfile.User.DateOfBirth,
            Bio = request.NannyProfile.Bio,
            YearsOfExperience = request.NannyProfile.YearsOfExperience,
            EducationLevel = (int?)request.NannyProfile.EducationLevel,
            VerificationStatus = (int)request.NannyProfile.VerificationStatus,
            ExpectedSalaryMin = request.NannyProfile.ExpectedSalaryMin,
            ExpectedSalaryMax = request.NannyProfile.ExpectedSalaryMax,
            SalaryType = request.NannyProfile.SalaryType,
            MaxTravelDistance = request.NannyProfile.MaxTravelDistance,
            Skills = request.NannyProfile.NannySkills
                .Where(skill => !skill.IsDeleted)
                .OrderBy(skill => skill.Skill.Category)
                .ThenBy(skill => skill.Skill.Name)
                .Select(skill => new VerificationSkillDto
                {
                    Id = skill.Id,
                    SkillId = skill.SkillId,
                    SkillName = skill.Skill.Name,
                    SkillCategory = skill.Skill.Category,
                    ProficiencyLevel = skill.ProficiencyLevel
                }).ToList(),
            Certificates = request.NannyProfile.NannyCertificates
                .Where(certificate => !certificate.IsDeleted)
                .OrderByDescending(certificate => certificate.IssueDate)
                .ThenBy(certificate => certificate.Name)
                .Select(certificate => new VerificationCertificateDto
                {
                    Id = certificate.Id,
                    Name = certificate.Name,
                    IssuingOrganization = certificate.IssuingOrganization,
                    IssueDate = certificate.IssueDate,
                    ExpiryDate = certificate.ExpiryDate,
                    CertificateUrl = certificate.CertificateUrl,
                    VerificationStatus = certificate.VerificationStatus
                }).ToList(),
            Documents = request.VerificationDocuments
                .Select(document => new VerificationDocumentDto
                {
                    Id = document.Id,
                    DocumentType = document.DocumentType,
                    DocumentUrl = document.DocumentUrl,
                    FileName = document.FileName,
                    FileSize = document.FileSize
                }).ToList()
        };
    }
}
