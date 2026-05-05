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
    public HiringService(IHiringRepository repo, ICommunicationService commSvc)
    {
        _repo = repo;
        _commSvc = commSvc;
    }

    public async Task<List<HiringRecordListItemDto>> GetMyHiringRecordsAsync(Guid userId)
    {
        var records = await _repo.GetHiringRecordsByUserIdAsync(userId);
        return records.Select(h =>
        {
            var contract = h.Contracts.FirstOrDefault(c => !c.IsDeleted);
            return new HiringRecordListItemDto
            {
                HiringRecordId = h.Id,
                ContractId = contract?.Id,
                JobTitle = h.JobApplication?.JobPosting?.Title ?? "Công việc",
                ParentName = GetDisplayName(h.ParentProfile?.User),
                ParentAvatar = h.ParentProfile?.User?.AvatarUrl,
                NannyName = GetDisplayName(h.NannyProfile?.User),
                NannyAvatar = h.NannyProfile?.User?.AvatarUrl,
                StartDate = h.StartDate,
                EndDate = h.EndDate,
                HiringStatus = h.Status,
                CreatedAt = h.CreatedAt
            };
        }).ToList();
    }

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

        var nannyUserId = app.NannyProfile?.UserId ?? Guid.Empty;
        if (nannyUserId != Guid.Empty)
        {
            _repo.AddNotification(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = nannyUserId,
                Title = "Đơn ứng tuyển được chấp nhận",
                Content = $"Parent đã chấp nhận đơn ứng tuyển của bạn cho bài đăng \"{app.JobPosting?.Title ?? "Công việc"}\".",
                Type = NotificationTypes.JobApplicationApproved,
                IsRead = false,
                RelatedEntityId = app.Id,
                RelatedEntityType = "JobApplication",
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            });
        }

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
        ValidateHiringDates(dto);

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
        var hiringRecord = CreatePendingHiringRecord(
            app,
            parentProfile,
            nannyProfile,
            parentUserId,
            dto.StartDate,
            dto.EndDate,
            now);

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

        AddHiringConfirmedNotifications(hiringRecord.Id, nannyUserId, parentUserId, GetDisplayName(parentProfile.User), now);

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

        var conversationId = await SendHiringOfferMessageAsync(parentUserId, nannyUserId, hiringRecord.Id);

        return new HiringConfirmedDto
        {
            HiringRecordId = hiringRecord.Id,
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
        ValidateHiringDates(dto);

        var request = await _repo.GetAcceptedContactRequestAsync(contactRequestId)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu liên hệ đã được chấp nhận.");

        if (request.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền tạo bản ghi thuê từ yêu cầu này.");

        var parentProfile = request.ParentProfile!;
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

        var hiringRecord = CreatePendingHiringRecord(
            directJobApplication, parentProfile, nannyProfile, parentUserId, dto.StartDate, dto.EndDate, now);

        var nannyUserId = nannyProfile.UserId;

        AddHiringConfirmedNotifications(hiringRecord.Id, nannyUserId, parentUserId, GetDisplayName(parentProfile.User), now);
        await _repo.SaveChangesAsync();

        var conversationId = await SendHiringOfferMessageAsync(parentUserId, nannyUserId, hiringRecord.Id);

        return new HiringConfirmedDto
        {
            HiringRecordId = hiringRecord.Id,
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
            ContractId = contract?.Id,
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

    public async Task CancelHiringRequestAsync(Guid hiringRecordId, Guid parentUserId)
    {
        var hiringRecord = await _repo.GetHiringRecordByIdAsync(hiringRecordId)
            ?? throw new KeyNotFoundException("Không tìm thấy đề nghị thuê.");

        if (hiringRecord.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền hủy đề nghị thuê này.");

        if (hiringRecord.Status != (int)HiringRecordStatus.Pending)
            throw new InvalidOperationException("Đề nghị này đã được xử lý trước đó.");

        var now = DateTime.UtcNow;
        hiringRecord.Status = (int)HiringRecordStatus.Cancelled;
        hiringRecord.UpdatedAt = now;
        hiringRecord.UpdatedBy = parentUserId;

        var nannyUserId = hiringRecord.NannyProfile?.UserId;
        if (nannyUserId.HasValue && nannyUserId.Value != Guid.Empty)
        {
            _repo.AddNotification(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = nannyUserId.Value,
                Title = "Thông báo từ NannyMatch",
                Content = $"Bố mẹ {GetDisplayName(hiringRecord.ParentProfile?.User)} đã hủy yêu cầu thuê.",
                Type = NotificationTypes.HiringCancelled,
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

    public async Task RespondHiringRequestAsync(Guid hiringRecordId, Guid nannyUserId, bool isAccepted)
    {
        var hiringRecord = await _repo.GetHiringRecordByIdAsync(hiringRecordId)
            ?? throw new KeyNotFoundException("Không tìm thấy đề nghị thuê.");

        if (hiringRecord.NannyProfile?.UserId != nannyUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền phản hồi đề nghị thuê này.");

        if (hiringRecord.Status != (int)HiringRecordStatus.Pending)
            throw new InvalidOperationException("Đề nghị này đã được xử lý trước đó.");

        var now = DateTime.UtcNow;
        hiringRecord.Status = isAccepted
            ? (int)HiringRecordStatus.Active
            : (int)HiringRecordStatus.Declined;
        hiringRecord.NannyConfirmedAt = now;
        hiringRecord.UpdatedAt = now;
        hiringRecord.UpdatedBy = nannyUserId;

        var parentUserId = hiringRecord.ParentProfile?.UserId;
        if (parentUserId.HasValue && parentUserId.Value != Guid.Empty)
        {
            _repo.AddNotification(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = parentUserId.Value,
                Title = "Thông báo từ NannyMatch",
                Content = isAccepted
                    ? $"Bảo mẫu {GetDisplayName(hiringRecord.NannyProfile?.User)} đã chấp nhận yêu cầu thuê."
                    : $"Bảo mẫu {GetDisplayName(hiringRecord.NannyProfile?.User)} đã từ chối yêu cầu thuê của bạn.",
                Type = isAccepted ? NotificationTypes.HiringAccepted : NotificationTypes.HiringDeclined,
                IsRead = false,
                RelatedEntityId = hiringRecord.Id,
                RelatedEntityType = "HiringRecord",
                CreatedAt = now,
                CreatedBy = nannyUserId,
                IsDeleted = false
            });
        }

        await _repo.SaveChangesAsync();
    }

    public async Task CompleteHiringAsync(Guid hiringRecordId, Guid parentUserId)
    {
        var hiringRecord = await _repo.GetHiringRecordByIdAsync(hiringRecordId)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        if (hiringRecord.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền hoàn thành hợp đồng này.");

        if (hiringRecord.Status != (int)HiringRecordStatus.Active)
            throw new InvalidOperationException("Chỉ có thể hoàn thành hợp đồng đang hoạt động.");

        if (!hiringRecord.EndDate.HasValue)
            throw new InvalidOperationException("Chưa đến hạn kết thúc hợp đồng thuê.");

        var todayBusinessDate = GetBusinessTodayDate();
        if (hiringRecord.EndDate.Value > todayBusinessDate)
            throw new InvalidOperationException("Chỉ được hoàn thành khi hợp đồng đã đến hạn kết thúc.");
        var now = DateTime.UtcNow;
        hiringRecord.Status = (int)HiringRecordStatus.Completed;
        hiringRecord.UpdatedAt = now;
        hiringRecord.UpdatedBy = parentUserId;

        var parentName = GetDisplayName(hiringRecord.ParentProfile?.User);
        var nannyName = GetDisplayName(hiringRecord.NannyProfile?.User);
        var completionMessage = $"Hợp đồng thuê giữa bố mẹ {parentName} và bảo mẫu {nannyName} đã hoàn thành";

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = parentUserId,
            Title = "Thông báo từ NannyMatch",
            Content = completionMessage,
            Type = NotificationTypes.HiringCompleted,
            IsRead = false,
            RelatedEntityId = hiringRecord.Id,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        var nannyUserId = hiringRecord.NannyProfile?.UserId;
        if (nannyUserId.HasValue && nannyUserId.Value != Guid.Empty)
        {
            _repo.AddNotification(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = nannyUserId.Value,
                Title = "Thông báo từ NannyMatch",
                Content = completionMessage,
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

    private static void ValidateHiringDates(ConfirmHiringDto dto)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (dto.StartDate < today)
            throw new ArgumentException("Ngày bắt đầu không được trước ngày hiện tại.");

        if (!dto.EndDate.HasValue)
            throw new ArgumentException("Vui lòng chọn ngày kết thúc.");

        if (dto.EndDate.Value <= dto.StartDate)
            throw new ArgumentException("Ngày kết thúc phải lớn hơn ngày bắt đầu.");
    }

    private HiringRecord CreatePendingHiringRecord(
        JobApplication jobApplication,
        ParentProfile parentProfile,
        NannyProfile nannyProfile,
        Guid parentUserId,
        DateOnly startDate,
        DateOnly? endDate,
        DateTime nowUtc)
    {
        var contractDurationMonths = CalculateContractDurationMonths(startDate, endDate);

        var hiringRecord = new HiringRecord
        {
            Id = Guid.NewGuid(),
            JobApplicationId = jobApplication.Id,
            ParentProfileId = parentProfile.Id,
            NannyProfileId = nannyProfile.Id,
            StartDate = startDate,
            EndDate = endDate,
            ContractDuration = contractDurationMonths,
            Status = (int)HiringRecordStatus.Pending,
            ParentConfirmedAt = nowUtc,
            CreatedAt = nowUtc,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddHiringRecord(hiringRecord);

        return hiringRecord;
    }

    public async Task<Guid> CreateContractForHiringAsync(Guid hiringRecordId, Guid parentUserId)
    {
        var hiringRecord = await _repo.GetHiringRecordByIdAsync(hiringRecordId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin tuyển dụng.");

        if (hiringRecord.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền tạo hợp đồng cho bản ghi này.");

        if (hiringRecord.Status != (int)HiringRecordStatus.Active)
            throw new InvalidOperationException("Chỉ có thể tạo hợp đồng khi bảo mẫu đã chấp nhận yêu cầu thuê.");

        var existingContract = await _repo.GetContractByHiringRecordIdAsync(hiringRecordId);
        if (existingContract != null)
            throw new InvalidOperationException("Hợp đồng đã được tạo cho bản ghi thuê này.");

        var parentProfile = hiringRecord.ParentProfile
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ phụ huynh.");
        var nannyProfile = hiringRecord.NannyProfile
            ?? throw new InvalidOperationException("Không tìm thấy hồ sơ bảo mẫu.");

        var now = DateTime.UtcNow;
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            HiringRecordId = hiringRecord.Id,
            ContractContent = BuildHardcodedContractContent(
                hiringRecord.JobApplication?.JobPosting,
                parentProfile.User,
                nannyProfile.User,
                hiringRecord.StartDate,
                hiringRecord.EndDate),
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
            Content = $"Bố mẹ {GetDisplayName(parentProfile.User)} đã tạo hợp đồng. Vui lòng xem và hoàn thành thông tin hợp đồng.",
            Type = NotificationTypes.ContractCreated,
            IsRead = false,
            RelatedEntityId = contract.Id,
            RelatedEntityType = "Contract",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        await _repo.SaveChangesAsync();
        return contract.Id;
    }

    private void AddHiringConfirmedNotifications(
        Guid hiringRecordId, Guid nannyUserId, Guid parentUserId, string parentDisplayName, DateTime now)
    {
        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = nannyUserId,
            Title = "Thông báo từ NannyMatch",
            Content = $"Bố mẹ {parentDisplayName} đã gửi đề nghị thuê bạn.",
            Type = NotificationTypes.HiringConfirmed,
            IsRead = false,
            RelatedEntityId = hiringRecordId,
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
            Content = "Bạn đã gửi đề nghị thuê bảo mẫu thành công.",
            Type = NotificationTypes.HiringConfirmed,
            IsRead = false,
            RelatedEntityId = hiringRecordId,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });
    }

    private async Task<Guid> SendHiringOfferMessageAsync(Guid parentUserId, Guid nannyUserId, Guid hiringRecordId)
    {
        try
        {
            var conversation = await _commSvc.GetOrCreateConversationAsync(parentUserId, nannyUserId);
            await _commSvc.SendMessageAsync(new SendMessageDto
            {
                ConversationId = conversation.Id,
                Content = "Đề nghị việc làm",
                MessageType = 4,
                AttachmentUrl = hiringRecordId.ToString()
            }, parentUserId);
            return conversation.Id;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    private static string BuildHardcodedContractContent(
        JobPosting? jobPosting,
        User? parentUser,
        User? nannyUser,
        DateOnly startDate,
        DateOnly? endDate)
    {
        // Contract content is hardcoded in backend and persisted directly into ContractContent.
        var durationMonths = CalculateContractDurationMonths(startDate, endDate);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["StartDate"] = startDate.ToString("dd/MM/yyyy"),
            ["EndDate"] = endDate?.ToString("dd/MM/yyyy") ?? string.Empty,
            ["ContractDurationMonths"] = durationMonths.ToString(),
            ["JobDescription"] = string.IsNullOrWhiteSpace(jobPosting?.Description) ? string.Empty : jobPosting!.Description.Trim(),
            ["WorkAddress"] = string.IsNullOrWhiteSpace(jobPosting?.Location) ? string.Empty : jobPosting.Location.Trim(),
            ["ParentName"] = GetDisplayName(parentUser),
            ["ParentPhone"] = parentUser?.PhoneNumber,
            ["ParentEmail"] = parentUser?.Email,
            ["NannyName"] = GetDisplayName(nannyUser),
            ["NannyPhone"] = nannyUser?.PhoneNumber,
            ["NannyEmail"] = nannyUser?.Email
        };

        return RenderTemplateWithValues(ContractTemplateDefaults.DefaultContent, values);
    }

    private static string RenderTemplateWithValues(string template, IDictionary<string, string?> values)
    {
        var content = template ?? string.Empty;
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            var token = $"{{{{{pair.Key.Trim()}}}}}";
            var value = string.IsNullOrWhiteSpace(pair.Value) ? "..." : pair.Value.Trim();
            content = content.Replace(token, value, StringComparison.OrdinalIgnoreCase);
        }

        return content.Replace("[[CENTER]]", string.Empty, StringComparison.Ordinal);
    }

    private static int CalculateContractDurationMonths(DateOnly startDate, DateOnly? endDate)
    {
        if (!endDate.HasValue)
            return 1;

        var totalDays = (endDate.Value.ToDateTime(TimeOnly.MinValue) - startDate.ToDateTime(TimeOnly.MinValue)).TotalDays;
        return Math.Max(1, (int)Math.Ceiling(totalDays / 30d));
    }

    private static string GetDisplayName(User? user)
    {
        if (user == null) return "Người dùng";
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static DateOnly GetBusinessTodayDate()
    {
        try
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nowInVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
            return DateOnly.FromDateTime(nowInVietnam.Date);
        }
        catch
        {
            return DateOnly.FromDateTime(DateTime.Now.Date);
        }
    }
}
