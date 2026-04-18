using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class VerificationRequestService
{
    private readonly VerificationRequestRepository _nannyVerificationRequestRepo;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly NotificationService _notificationService;

    public VerificationRequestService(
        VerificationRequestRepository nannyVerificationRequestRepo,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        NotificationService notificationService)
    {
        _nannyVerificationRequestRepo = nannyVerificationRequestRepo;
        _env = env;
        _notificationService = notificationService;
    }
    //Nanny view own verification request list
    public async Task<VerificationRequestListResponse> NannyGetVerificationRequestListAsync(Guid userId, int? status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 3;

        var profile = await _nannyVerificationRequestRepo.GetNannyProfileByUserIdAsync(userId);
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

        var requests = await _nannyVerificationRequestRepo.GetRequestsByNannyProfileAsync(profile.Id);
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

    // Nanny view own verification request detail
    public async Task<(bool Success, VerificationRequestDetailDto? Data, string? Message)> NannyViewVerificationRequestDetailAsync(Guid userId, Guid id)
    {
        var profile = await _nannyVerificationRequestRepo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
        {
            return (false, null, "Khong tim thay ho so Nanny.");
        }

        var request = await _nannyVerificationRequestRepo.GetByIdAsync(id);
        if (request == null || request.NannyProfileId != profile.Id)
        {
            return (false, null, "Khong tim thay yeu cau xac minh.");
        }

        return (true, MapDetailDto(request), null);
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

    // Nanny submit new verification request
    public async Task<(bool Success, string Message)> NannySubmitVerificationRequestAsync(Guid userId, SubmitVerificationRequestDto request)
    {
        var profile = await _nannyVerificationRequestRepo.GetNannyProfileByUserIdAsync(userId);
        if (profile == null)
        {
            return (false, "Khong tim thay ho so Nanny.");
        }

        var existingRequests = await _nannyVerificationRequestRepo.GetRequestsByNannyProfileAsync(profile.Id);
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

        _nannyVerificationRequestRepo.AddRequest(verificationRequest);

        profile.VerificationStatus = (int)VerificationStatus.Pending;
        profile.VerifiedAt = DateTime.UtcNow;
        profile.VerifiedBy = null;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.UpdatedBy = userId;

        await _nannyVerificationRequestRepo.SaveChangesAsync();

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
}
