using Microsoft.Extensions.DependencyInjection;
using Nanny_BackEnd.DTOs.Communication;
using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class HiringService : IHiringService
{
    private readonly IHiringRepository _repo;
    private readonly ICommunicationService _commSvc;

    // Constructor dùng cho DI (Production) — được chọn bởi ASP.NET DI
    [ActivatorUtilitiesConstructor]
    public HiringService(IHiringRepository repo, ICommunicationService commSvc)
    {
        _repo = repo;
        _commSvc = commSvc;
    }

    // Constructor dùng cho Test (tương thích với GetApplicantsTests)
    public HiringService(IHiringRepository repo, CommunicationService _svc) => _repo = repo;

   
    public async Task<List<JobApplicantDto>> GetApplicantsAsync(Guid jobPostingId, Guid parentUserId)
    {
        var job = await _repo.GetJobPostingByIdAsync(jobPostingId)
            ?? throw new KeyNotFoundException("Không tìm thấy bài đăng.");

        if (job.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền xem danh sách ứng viên của bài đăng này.");

        var applications = await _repo.GetApplicantsByJobPostingIdAsync(jobPostingId);
        return applications.Select(a => new JobApplicantDto
        {
            JobApplicationId = a.Id,
            NannyUserId = a.NannyProfile?.UserId ?? Guid.Empty,
            NannyProfileId = a.NannyProfileId,
            NannyName = GetDisplayName(a.NannyProfile?.User),
            NannyAvatar = a.NannyProfile?.User?.AvatarUrl,
            NannyRating = a.NannyProfile?.AverageRating,
            YearsOfExperience = a.NannyProfile?.YearsOfExperience,
            ExpectedSalaryMin = a.NannyProfile?.ExpectedSalaryMin,
            ExpectedSalaryMax = a.NannyProfile?.ExpectedSalaryMax,
            AppliedAt = a.CreatedAt,
            Status = a.Status,
            RejectionReason = a.RejectionReason
        }).ToList();
    }

    public async Task ApproveApplicantAsync(Guid jobPostingId, Guid jobAppId, Guid parentUserId)
    {
        var app = await GetVerifiedApplicationAsync(jobPostingId, jobAppId, parentUserId);

        if (app.Status is 2 or 3 or 4)
            throw new InvalidOperationException("Ứng viên này đã được xử lý, không thể đồng ý nữa.");

        var now = DateTime.UtcNow;
        app.Status = 1;
        app.ReviewedAt = now;
        app.UpdatedAt = now;
        app.UpdatedBy = parentUserId;
        await _repo.SaveChangesAsync();
    }

    public async Task<NannyHireContextDto> GetNannyHireContextAsync(Guid jobPostingId, Guid jobAppId, Guid parentUserId)
    {
        var app = await _repo.GetJobApplicationByIdAsync(jobAppId)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn ứng tuyển.");

        var canHire = app.JobPosting?.ParentProfile?.UserId == parentUserId &&
                      app.JobPostingId == jobPostingId &&
                      app.Status == 1;

        var nanny = app.NannyProfile;
        return new NannyHireContextDto
        {
            CanHire = canHire,
            JobApplicationId = canHire ? app.Id : null,
            JobPostingId = canHire ? app.JobPostingId : null,
            NannyUserId = nanny?.UserId ?? Guid.Empty,
            NannyProfileId = nanny?.Id ?? Guid.Empty,
            NannyName = GetDisplayName(nanny?.User),
            NannyAvatar = nanny?.User?.AvatarUrl
        };
    }

    public async Task<HiringConfirmedDto> ConfirmHiringAsync(
        Guid jobPostingId, Guid jobAppId, Guid parentUserId, ConfirmHiringDto dto)
    {
        ValidateContractDates(dto);

        var app = await GetVerifiedApplicationAsync(jobPostingId, jobAppId, parentUserId);
        if (app.Status != 1)
            throw new InvalidOperationException("Ứng viên này chưa được đồng ý trước khi thuê.");

        var latestHiring = await _repo.GetLatestHiringRecordByJobApplicationIdAsync(jobAppId);
        if (latestHiring != null && latestHiring.Status is 0 or 1)
            throw new InvalidOperationException("Ứng viên này đang có đề nghị hoặc hợp đồng đang hiệu lực.");

        var parentProfile = await _repo.GetParentProfileByUserIdAsync(parentUserId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ phụ huynh.");

        var nannyProfile = app.NannyProfile
            ?? throw new InvalidOperationException("Không tìm thấy thông tin bảo mẫu.");

        var now = DateTime.UtcNow;

        var hiringRecord = new HiringRecord
        {
            Id = Guid.NewGuid(),
            JobApplicationId = app.Id,
            ParentProfileId = parentProfile.Id,
            NannyProfileId = nannyProfile.Id,
            ContractTemplateId = null,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ContractDuration = null,
            Status = (int)HiringRecordStatus.Pending,
            ParentConfirmedAt = now,
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddHiringRecord(hiringRecord);

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            HiringRecordId = hiringRecord.Id,
            ContractTemplateId = null,
            ContractContent = BuildSimpleContractContent(
                GetDisplayName(parentProfile.User),
                GetDisplayName(nannyProfile.User),
                dto.StartDate,
                dto.EndDate),
            SignedByParent = false,
            SignedByNanny = false,
            Status = 0,
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddContract(contract);

        var others = await _repo.GetOtherActiveApplicantsAsync(jobPostingId, jobAppId);
        foreach (var other in others)
        {
            other.Status = 3;
            other.RejectionReason = "Vị trí đã được tuyển dụng.";
            other.ReviewedAt = now;
            other.UpdatedAt = now;
            other.UpdatedBy = parentUserId;
        }

        var nannyUserId = nannyProfile.UserId;

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = nannyUserId,
            Title = "Thông báo từ NannyMatch",
            Content = $"Bố mẹ {GetDisplayName(parentProfile.User)} đã thuê bạn.",
            Type = NotificationTypes.HiringConfirmed,
            IsRead = false,
            RelatedEntityId = hiringRecord.Id,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = parentUserId,
            Title = "Thông báo từ NannyMatch",
            Content = "Bạn đã xác nhận thuê bảo mẫu thành công.",
            Type = NotificationTypes.HiringConfirmed,
            IsRead = false,
            RelatedEntityId = hiringRecord.Id,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        foreach (var other in others)
        {
            var otherUserId = other.NannyProfile?.UserId;
            if (!otherUserId.HasValue || otherUserId.Value == Guid.Empty)
                continue;

            _repo.AddNotification(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = otherUserId.Value,
                Title = "Đơn ứng tuyển không được chọn",
                Content = "Vị trí đã được tuyển dụng bởi ứng viên khác.",
                Type = NotificationTypes.JobApplicationRejected,
                IsRead = false,
                RelatedEntityId = other.JobPostingId,
                RelatedEntityType = "JobPosting",
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            });
        }

        await _repo.SaveChangesAsync();

        // Gửi tin nhắn "Đề nghị việc làm" (type 4) vào chat giữa parent và nanny
        var conversationId = Guid.Empty;
        try
        {
            var conversation = await _commSvc.GetOrCreateConversationAsync(parentUserId, nannyUserId);
            conversationId = conversation.Id;
            await _commSvc.SendMessageAsync(new SendMessageDto
            {
                ConversationId = conversationId,
                Content = "Đề nghị việc làm",
                MessageType = 4, // HiringOffer
                AttachmentUrl = hiringRecord.Id.ToString()
            }, parentUserId);
        }
        catch { /* không chặn flow chính nếu gửi tin nhắn thất bại */ }

        return new HiringConfirmedDto
        {
            HiringRecordId = hiringRecord.Id,
            ContractId = contract.Id,
            ConversationId = conversationId,
            ParentUserId = parentUserId,
            NannyUserId = nannyUserId,
            ParentName = GetDisplayName(parentProfile.User),
            BatchRejectedCount = others.Count
        };
    }

    public async Task<HiringConfirmedDto> ConfirmHiringByContactRequestAsync(
        Guid contactRequestId, Guid parentUserId, ConfirmHiringDto dto)
    {
        ValidateContractDates(dto);

        var request = await _repo.GetAcceptedContactRequestAsync(contactRequestId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ đã được chấp nhận.");

        if (request.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền tạo bản ghi thuê từ yêu cầu này.");

        var parentProfile = request.ParentProfile
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ phụ huynh.");
        var nannyProfile = request.NannyProfile
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ bảo mẫu.");

        var now = DateTime.UtcNow;

        var directJobPosting = new JobPosting
        {
            Id = Guid.NewGuid(),
            ParentProfileId = parentProfile.Id,
            Title = "Thue bao mau truc tiep",
            Description = string.IsNullOrWhiteSpace(request.Message)
                ? "Tạo từ yêu cầu liên hệ đã được chấp nhận."
                : request.Message.Trim(),
            JobType = 0,
            SalaryMin = nannyProfile.ExpectedSalaryMin,
            SalaryMax = nannyProfile.ExpectedSalaryMax,
            SalaryType = nannyProfile.SalaryType > 0 ? nannyProfile.SalaryType : 2,
            SalaryNegotiable = true,
            NumberOfChildren = null,
            Location = null,
            City = parentProfile.User?.City,
            District = parentProfile.User?.District,
            Latitude = parentProfile.User?.Latitude,
            Longitude = parentProfile.User?.Longitude,
            Status = 2,
            ModerationStatus = 1,
            PublishedAt = now,
            ClosedAt = now,
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddJobPosting(directJobPosting);

        var directJobApplication = new JobApplication
        {
            Id = Guid.NewGuid(),
            JobPostingId = directJobPosting.Id,
            NannyProfileId = nannyProfile.Id,
            Status = 2,
            ReviewedAt = now,
            CreatedAt = now,
            CreatedBy = nannyProfile.UserId,
            UpdatedAt = now,
            UpdatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddJobApplication(directJobApplication);

        var hiringRecord = new HiringRecord
        {
            Id = Guid.NewGuid(),
            JobApplicationId = directJobApplication.Id,
            ParentProfileId = parentProfile.Id,
            NannyProfileId = nannyProfile.Id,
            ContractTemplateId = null,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ContractDuration = null,
            Status = (int)HiringRecordStatus.Pending,
            ParentConfirmedAt = now,
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddHiringRecord(hiringRecord);

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            HiringRecordId = hiringRecord.Id,
            ContractTemplateId = null,
            ContractContent = BuildSimpleContractContent(
                GetDisplayName(parentProfile.User),
                GetDisplayName(nannyProfile.User),
                dto.StartDate,
                dto.EndDate),
            SignedByParent = false,
            SignedByNanny = false,
            Status = 0,
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddContract(contract);

        var nannyUserId = nannyProfile.UserId;

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = nannyUserId,
            Title = "Thông báo từ NannyMatch",
            Content = $"Bố mẹ {GetDisplayName(parentProfile.User)} đã thuê bạn.",
            Type = NotificationTypes.HiringConfirmed,
            IsRead = false,
            RelatedEntityId = hiringRecord.Id,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = parentUserId,
            Title = "Thông báo từ NannyMatch",
            Content = "Bạn đã xác nhận thuê bảo mẫu thành công.",
            Type = NotificationTypes.HiringConfirmed,
            IsRead = false,
            RelatedEntityId = hiringRecord.Id,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        await _repo.SaveChangesAsync();

        // Gửi tin nhắn "Đề nghị việc làm" (type 4) vào chat giữa parent và nanny
        var conversationId = Guid.Empty;
        try
        {
            var conversation = await _commSvc.GetOrCreateConversationAsync(parentUserId, nannyUserId);
            conversationId = conversation.Id;
            await _commSvc.SendMessageAsync(new SendMessageDto
            {
                ConversationId = conversationId,
                Content = "Đề nghị việc làm",
                MessageType = 4, // HiringOffer
                AttachmentUrl = hiringRecord.Id.ToString()
            }, parentUserId);
        }
        catch { /* không chặn flow chính nếu gửi tin nhắn thất bại */ }

        return new HiringConfirmedDto
        {
            HiringRecordId = hiringRecord.Id,
            ContractId = contract.Id,
            ConversationId = conversationId,
            ParentUserId = parentUserId,
            NannyUserId = nannyUserId,
            ParentName = GetDisplayName(parentProfile.User),
            BatchRejectedCount = 0
        };
    }

    public async Task<HiringOfferDetailDto> GetHiringOfferDetailAsync(Guid hiringRecordId, Guid currentUserId)
    {
        var hiringRecord = await _repo.GetHiringRecordByIdAsync(hiringRecordId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin tuyển dụng.");

        var parentUserId = hiringRecord.ParentProfile?.UserId;
        var nannyUserId = hiringRecord.NannyProfile?.UserId;
        if (currentUserId != parentUserId && currentUserId != nannyUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền xem thông tin tuyển dụng này.");

        var contract = await _repo.GetContractByHiringRecordIdAsync(hiringRecordId);

        return new HiringOfferDetailDto
        {
            HiringRecordId = hiringRecord.Id,
            ContractId = contract?.Id ?? Guid.Empty,
            JobPostingId = hiringRecord.JobApplication?.JobPostingId ?? Guid.Empty,
            JobPostingTitle = hiringRecord.JobApplication?.JobPosting?.Title ?? string.Empty,
            ParentName = GetDisplayName(hiringRecord.ParentProfile?.User),
            ParentAvatar = hiringRecord.ParentProfile?.User?.AvatarUrl,
            NannyName = GetDisplayName(hiringRecord.NannyProfile?.User),
            StartDate = hiringRecord.StartDate,
            EndDate = hiringRecord.EndDate,
            ContractDuration = hiringRecord.ContractDuration,
            ContractContent = contract?.ContractContent ?? string.Empty,
            SignedByParent = contract?.SignedByParent ?? false,
            SignedByNanny = contract?.SignedByNanny ?? false,
            Status = hiringRecord.Status,
            CreatedAt = hiringRecord.CreatedAt,
            SignedAt = contract?.SignedAt
        };
    }

    public async Task RespondToOfferAsync(Guid hiringRecordId, Guid nannyUserId, RespondToOfferDto dto)
    {
        var hiringRecord = await _repo.GetHiringRecordByIdAsync(hiringRecordId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin tuyển dụng.");

        if (hiringRecord.NannyProfile?.UserId != nannyUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền phản hồi đề nghị này.");

        var action = (dto.Action ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Hành động không hợp lệ. Vui lòng chọn 'accept'.");

        if (hiringRecord.Status != (int)HiringRecordStatus.Pending)
            throw new InvalidOperationException("Đề nghị này đã được xử lý trước đó.");

        var contract = await _repo.GetContractByHiringRecordIdAsync(hiringRecordId)
            ?? throw new InvalidOperationException("Không tìm thấy hợp đồng liên quan.");

        var now = DateTime.UtcNow;
        var parentUserId = hiringRecord.ParentProfile?.UserId ?? Guid.Empty;
        var jobApp = hiringRecord.JobApplication;
        var jobPosting = jobApp?.JobPosting;

        if (action.Equals("accept", StringComparison.OrdinalIgnoreCase))
        {
            hiringRecord.Status = (int)HiringRecordStatus.Active;
            hiringRecord.NannyConfirmedAt = now;

            contract.SignedByNanny = false;
            contract.SignedAt = null;
            contract.Status = 0;

            if (jobApp != null)
            {
                jobApp.Status = 2;
                jobApp.UpdatedAt = now;
                jobApp.UpdatedBy = nannyUserId;
            }

            if (jobPosting != null)
            {
                jobPosting.Status = 2;
                jobPosting.ClosedAt = now;
                jobPosting.UpdatedAt = now;
                jobPosting.UpdatedBy = nannyUserId;
            }

            if (jobPosting != null && jobApp != null)
            {
                var otherPendingApplications = await _repo.GetOtherPendingApplicantsAsync(jobPosting.Id, jobApp.Id);
                foreach (var pendingApplication in otherPendingApplications)
                {
                    pendingApplication.Status = 2;
                pendingApplication.RejectionReason = "Vị trí đã được bảo mẫu khác chấp nhận đề nghị.";
                    pendingApplication.ReviewedAt = now;
                    pendingApplication.UpdatedAt = now;
                    pendingApplication.UpdatedBy = nannyUserId;
                }
            }

            if (parentUserId != Guid.Empty)
            {
                _repo.AddNotification(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = parentUserId,
                    Title = "Bảo mẫu đã chấp nhận đề nghị!",
                    Content = $"{GetDisplayName(hiringRecord.NannyProfile?.User)} đã chấp nhận đề nghị.",
                    Type = NotificationTypes.HiringAccepted,
                    IsRead = false,
                    RelatedEntityId = hiringRecord.Id,
                    RelatedEntityType = "HiringRecord",
                    CreatedAt = now,
                    CreatedBy = nannyUserId,
                    IsDeleted = false
                });
            }
        }
        else
        {
            throw new ArgumentException("Hành động không hợp lệ. Vui lòng chọn 'accept'.");
        }

        hiringRecord.UpdatedAt = now;
        hiringRecord.UpdatedBy = nannyUserId;
        contract.UpdatedAt = now;
        contract.UpdatedBy = nannyUserId;

        await _repo.SaveChangesAsync();
    }

    public async Task CompleteHiringAsync(Guid hiringRecordId, Guid parentUserId)
    {
        var hiringRecord = await _repo.GetHiringRecordByIdAsync(hiringRecordId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        if (hiringRecord.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền hoàn thành hợp đồng này.");

        if (hiringRecord.Status != (int)HiringRecordStatus.Active)
            throw new InvalidOperationException("Chi co the hoan thanh hop dong dang hoat dong.");

        var now = DateTime.UtcNow;
        hiringRecord.Status = (int)HiringRecordStatus.Completed;
        hiringRecord.UpdatedAt = now;
        hiringRecord.UpdatedBy = parentUserId;

        var nannyUserId = hiringRecord.NannyProfile?.UserId;
        if (nannyUserId.HasValue && nannyUserId.Value != Guid.Empty)
        {
            _repo.AddNotification(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = nannyUserId.Value,
                Title = "Hợp đồng đã hoàn thành",
                Content = $"{GetDisplayName(hiringRecord.ParentProfile?.User)} đã xác nhận hợp đồng hoàn thành.",
                Type = NotificationTypes.HiringCompleted,
                IsRead = false,
                RelatedEntityId = hiringRecord.Id,
                RelatedEntityType = "HiringRecord",
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            });
        }

        await _repo.SaveChangesAsync();
    }

    private async Task<JobApplication> GetVerifiedApplicationAsync(Guid jobPostingId, Guid jobAppId, Guid parentUserId)
    {
        var app = await _repo.GetJobApplicationByIdAsync(jobAppId)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn ứng tuyển.");

        if (app.JobPostingId != jobPostingId)
            throw new ArgumentException("Đơn ứng tuyển không thuộc bài đăng này.");
        if (app.JobPosting?.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền thực hiện hành động này với đơn ứng tuyển này.");

        return app;
    }

    private static void ValidateContractDates(ConfirmHiringDto dto)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (dto.StartDate < today)
            throw new ArgumentException("Ngày bắt đầu không được trước ngày tạo hợp đồng.");

        if (!dto.EndDate.HasValue)
            throw new ArgumentException("Vui lòng chọn ngày kết thúc.");

        if (dto.EndDate.Value <= dto.StartDate)
            throw new ArgumentException("Ngày kết thúc phải lớn hơn ngày bắt đầu.");
    }

    private static string BuildSimpleContractContent(
        string parentName,
        string nannyName,
        DateOnly startDate,
        DateOnly? endDate)
    {
        var safeParentName = string.IsNullOrWhiteSpace(parentName) ? "..." : parentName.Trim();
        var safeNannyName = string.IsNullOrWhiteSpace(nannyName) ? "..." : nannyName.Trim();
        var safeEndDate = endDate.HasValue ? endDate.Value.ToString("dd/MM/yyyy") : "...";

        return $"Hợp đồng giữa Bố mẹ {safeParentName} và bảo mẫu {safeNannyName}, ngày bắt đầu là: {startDate:dd/MM/yyyy}, ngày kết thúc là: {safeEndDate}.";
    }

    private static string GetDisplayName(User? user)
    {
        if (user == null) return "Người dùng";
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
