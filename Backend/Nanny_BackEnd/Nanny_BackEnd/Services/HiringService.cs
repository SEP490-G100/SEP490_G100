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

    public async Task<List<ContractTemplateOptionDto>> GetContractTemplatesAsync()
    {
        var templates = await _repo.GetActiveContractTemplatesAsync();
        return templates.Select(t => new ContractTemplateOptionDto
        {
            Id = t.Id,
            Name = t.Name,
            Version = t.Version
        }).ToList();
    }

    public async Task<ContractTemplatePreviewDto> GetContractTemplatePreviewAsync(Guid templateId)
    {
        var template = await _repo.GetActiveContractTemplateByIdAsync(templateId)
            ?? throw new KeyNotFoundException("Không tìm thấy mẫu hợp đồng.");

        return new ContractTemplatePreviewDto
        {
            Id = template.Id,
            Name = template.Name,
            Version = template.Version,
            Content = template.Content ?? string.Empty
        };
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
        var (hiringRecord, contract) = CreatePendingHiringAndContract(
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

        var (hiringRecord, contract) = CreatePendingHiringAndContract(
            directJobApplication,
            parentProfile,
            nannyProfile,
            parentUserId,
            dto.StartDate,
            dto.EndDate,
            now);

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

    private (HiringRecord HiringRecord, Contract Contract) CreatePendingHiringAndContract(
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

        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            HiringRecordId = hiringRecord.Id,
            ContractContent = BuildHardcodedContractContent(
                jobApplication.JobPosting,
                parentProfile.User,
                nannyProfile.User,
                startDate,
                endDate),
            SignedByParent = false,
            SignedByNanny = false,
            Status = 0,
            CreatedAt = nowUtc,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddContract(contract);

        return (hiringRecord, contract);
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
