using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class VerificationRequestService : IVerificationRequestService
{
    private readonly IVerificationRequestRepository _verificationRequestRepo;
    private readonly INotificationService _notificationService;

    public VerificationRequestService(
        IVerificationRequestRepository verificationRequestRepo,
        INotificationService notificationService)
    {
        _verificationRequestRepo = verificationRequestRepo;
        _notificationService = notificationService;
    }

    public async Task<VerificationRequestListResponse> ModeratorViewVerificationListAsync(
        int? status,
        int? requestType,
        string? search,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 3;

        var (items, totalCount) = await _verificationRequestRepo.GetModeratorListAsync(status, requestType, search, page, pageSize);

        return new VerificationRequestListResponse
        {
            Items = items.Select(MapModeratorListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, VerificationRequestDetailDto? Data, string? Message)> ModeratorViewVerificationDetailAsync(Guid id)
    {
        var request = await _verificationRequestRepo.GetByIdAsync(id);
        if (request == null)
            return (false, null, "Không tìm thấy yêu cầu xác minh.");

        return (true, MapDetailDto(request), null);
    }

    public async Task<(bool Success, int StatusCode, string Message)> ModeratorReviewVerificationAsync(
        Guid id,
        Guid moderatorId,
        ReviewVerificationRequest request)
    {
        if (request.Action != (int)NannyVerificationRequestStatus.Approved &&
            request.Action != (int)NannyVerificationRequestStatus.Rejected)
        {
            return (false, 400, "Action không hợp lệ. Chỉ chấp nhận 2 (Approve) hoặc 3 (Reject).");
        }

        if (request.Action == (int)NannyVerificationRequestStatus.Rejected &&
            string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return (false, 400, "Lý do từ chối là bắt buộc khi từ chối yêu cầu.");
        }

        var verificationRequest = await _verificationRequestRepo.GetByIdAsync(id);
        if (verificationRequest == null)
            return (false, 404, "Không tìm thấy yêu cầu xác minh.");

        if (verificationRequest.Status != (int)NannyVerificationRequestStatus.Pending)
            return (false, 409, "Yêu cầu này đã được xử lý trước đó.");

        verificationRequest.Status = request.Action;
        verificationRequest.ReviewedBy = moderatorId;
        verificationRequest.ReviewedAt = DateTime.UtcNow;
        verificationRequest.RejectionReason = request.Action == (int)NannyVerificationRequestStatus.Rejected
            ? request.RejectionReason?.Trim()
            : null;
        verificationRequest.UpdatedAt = DateTime.UtcNow;
        verificationRequest.UpdatedBy = moderatorId;

        if (request.Action == (int)NannyVerificationRequestStatus.Approved)
        {
            var reviewTime = verificationRequest.ReviewedAt ?? DateTime.UtcNow;
            foreach (var document in verificationRequest.VerificationDocuments.Where(document =>
                         !document.IsDeleted &&
                         document.DocumentType == (int)VerificationDocumentType.HealthCertificate))
            {
                document.ExpiryDate = reviewTime.AddMonths(6);
                document.UpdatedAt = DateTime.UtcNow;
                document.UpdatedBy = moderatorId;
            }
        }

        var nannyProfile = await _verificationRequestRepo.GetNannyProfileAsync(verificationRequest.NannyProfileId);
        if (nannyProfile != null && verificationRequest.RequestType == (int)VerificationRequestType.ProfileVerification)
        {
            nannyProfile.VerificationStatus = request.Action == (int)NannyVerificationRequestStatus.Approved
                ? (int)VerificationStatus.Approved
                : (int)VerificationStatus.Rejected;
            nannyProfile.VerifiedAt = DateTime.UtcNow;
            nannyProfile.VerifiedBy = request.Action == (int)NannyVerificationRequestStatus.Approved
                ? moderatorId
                : null;
            nannyProfile.UpdatedAt = DateTime.UtcNow;
            nannyProfile.UpdatedBy = moderatorId;
        }

        await _verificationRequestRepo.SaveChangesAsync();

        var notificationTitle = request.Action == (int)NannyVerificationRequestStatus.Approved
            ? "Yêu cầu xác minh của bạn đã được chấp thuận"
            : "Yêu cầu xác minh của bạn đã bị từ chối";
        var notificationContent = request.Action == (int)NannyVerificationRequestStatus.Approved
            ? "Moderator đã chấp thuận yêu cầu xác minh của bạn."
            : $"Moderator đã từ chối yêu cầu xác minh của bạn. {(string.IsNullOrWhiteSpace(verificationRequest.RejectionReason) ? string.Empty : $"Lý do: {verificationRequest.RejectionReason}")}".Trim();
        var notificationType = request.Action == (int)NannyVerificationRequestStatus.Approved
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

        var message = request.Action == (int)NannyVerificationRequestStatus.Approved
            ? "Đã duyệt yêu cầu xác minh."
            : "Đã từ chối yêu cầu xác minh.";

        return (true, 200, message);
    }

    public async Task<VerificationRequestListResponse> NannyGetVerificationRequestListAsync(Guid userId, int? status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 3;

        var profile = await _verificationRequestRepo.GetNannyProfileByUserIdAsync(userId);
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

        var requests = await _verificationRequestRepo.GetRequestsByNannyProfileAsync(profile.Id);
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

    public async Task<(bool Success, VerificationRequestDetailDto? Data, string? Message)> NannyViewVerificationRequestDetailAsync(Guid userId, Guid id)
    {
        var profile = await _verificationRequestRepo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
        {
            return (false, null, "Không tìm thấy hồ sơ Nanny.");
        }

        var request = await _verificationRequestRepo.GetByIdAsync(id);
        if (request == null || request.NannyProfileId != profile.Id)
        {
            return (false, null, "Không tìm thấy yêu cầu xác minh.");
        }

        return (true, MapDetailDto(request), null);
    }

    public async Task<(bool Success, string Message)> NannySubmitVerificationRequestAsync(Guid userId, SubmitVerificationRequestDto request)
    {
        var profile = await _verificationRequestRepo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
        {
            return (false, "Không tìm thấy hồ sơ Nanny.");
        }

        if (request.Documents == null || !request.Documents.Any())
        {
            return (false, "Ban phai tai len it nhat mot tai lieu.");
        }

        var requestType = Enum.IsDefined(typeof(VerificationRequestType), request.RequestType)
            ? (VerificationRequestType)request.RequestType
            : VerificationRequestType.ProfileVerification;

        var existingRequests = await _verificationRequestRepo.GetRequestsByNannyProfileAsync(profile.Id);
        if (existingRequests.Any(existingRequest =>
                existingRequest.Status == (int)NannyVerificationRequestStatus.Pending &&
                existingRequest.RequestType == (int)requestType))
        {
            return (false, "Bạn đã có một yêu cầu đang chờ duyệt cho loại hồ sơ này.");
        }

        if (requestType == VerificationRequestType.HealthCertificate && !request.HealthCertificateExpiryDate.HasValue)
        {
            return (false, "Ban phai nhap ngay het han cho giay kham suc khoe.");
        }

        var verificationRequest = new Nanny_BackEnd.Models.VerificationRequest
        {
            Id = Guid.NewGuid(),
            NannyProfileId = profile.Id,
            RequestType = (int)requestType,
            Status = (int)NannyVerificationRequestStatus.Pending,
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
                ExpiryDate = document.ExpiryDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }

        _verificationRequestRepo.AddRequest(verificationRequest);

        if (requestType == VerificationRequestType.ProfileVerification)
        {
            profile.VerificationStatus = (int)VerificationStatus.Pending;
            profile.VerifiedAt = DateTime.UtcNow;
            profile.VerifiedBy = null;
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = userId;
        }

        await _verificationRequestRepo.SaveChangesAsync();

        await _notificationService.createNotificationForModerators(
            "Có yêu cầu xác minh mới",
            "Một nanny vừa gửi yêu cầu xác minh mới và đang chờ moderator xem xét.",
            NotificationTypes.VerificationRequestSubmitted,
            verificationRequest.Id,
            "VerificationRequest",
            userId);

        return (true, "Gửi yêu cầu xác minh thành công.");
    }

    private static VerificationRequestListDto MapListDto(Nanny_BackEnd.Models.VerificationRequest request)
    {
        return new VerificationRequestListDto
        {
            Id = request.Id,
            NannyProfileId = request.NannyProfileId,
            RequestType = request.RequestType,
            DocumentTypes = request.VerificationDocuments
                .Where(document => !document.IsDeleted)
                .Select(document => document.DocumentType)
                .Distinct()
                .OrderBy(documentType => documentType)
                .ToList(),
            ExpiryDate = request.VerificationDocuments
                .Where(document => !document.IsDeleted && document.ExpiryDate.HasValue)
                .Select(document => document.ExpiryDate)
                .Max(),
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

    private static VerificationRequestListDto MapModeratorListDto(Nanny_BackEnd.Models.VerificationRequest request)
    {
        var activeDocuments = request.VerificationDocuments.Where(document => !document.IsDeleted).ToList();
        var healthCertDocument = activeDocuments
            .Where(document => document.DocumentType == (int)VerificationDocumentType.HealthCertificate)
            .OrderByDescending(document => document.ExpiryDate)
            .FirstOrDefault();

        return new VerificationRequestListDto
        {
            Id = request.Id,
            NannyProfileId = request.NannyProfileId,
            RequestType = request.RequestType,
            DocumentTypes = activeDocuments
                .Select(document => document.DocumentType)
                .Distinct()
                .ToList(),
            ExpiryDate = healthCertDocument?.ExpiryDate,
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
            RequestType = request.RequestType,
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
                    FileSize = document.FileSize,
                    ExpiryDate = document.ExpiryDate
                }).ToList()
        };
    }
}
