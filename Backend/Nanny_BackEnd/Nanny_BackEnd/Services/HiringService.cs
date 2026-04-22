using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using System.Text.RegularExpressions;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class HiringService : IHiringService
{
    private readonly IHiringRepository _repo;
    private readonly ICommunicationService _communication;

    public HiringService(IHiringRepository repo, ICommunicationService communication)
    {
        _repo = repo;
        _communication = communication;
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
            Status = a.Status
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
        if (latestHiring != null && latestHiring.Status is 0 or 1 or 3)
            throw new InvalidOperationException("Ứng viên này đang có đề nghị hoặc hợp đồng đang hiệu lực.");

        var template = await _repo.GetTemplateByIdAsync(dto.ContractTemplateId)
            ?? throw new KeyNotFoundException("Mẫu hợp đồng không tồn tại hoặc không hoạt động.");

        var parentProfile = await _repo.GetParentProfileByUserIdAsync(parentUserId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ phụ huynh.");

        var nannyProfile = app.NannyProfile
            ?? throw new InvalidOperationException("Không tìm thấy thông tin bảo mẫu.");

        var now = DateTime.UtcNow;
        var contractNumber = await GenerateNextContractNumberAsync(now);

        var hiringRecord = new HiringRecord
        {
            Id = Guid.NewGuid(),
            JobApplicationId = app.Id,
            ParentProfileId = parentProfile.Id,
            NannyProfileId = nannyProfile.Id,
            ContractTemplateId = template.Id,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ContractDuration = dto.ContractDuration,
            Status = 0,
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
            ContractTemplateId = template.Id,
            ContractContent = RenderTemplate(template.Content, parentProfile, nannyProfile, dto, app, contractNumber),
            SignedByParent = true,
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
            other.RejectionReason = "Vi tri da duoc tuyen dung.";
            other.ReviewedAt = now;
            other.UpdatedAt = now;
            other.UpdatedBy = parentUserId;
        }

        var nannyUserId = nannyProfile.UserId;
        var conversation = await _repo.FindOneToOneConversationAsync(parentUserId, nannyUserId);
        if (conversation == null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = 1,
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            };
            _repo.AddConversation(conversation);

            _repo.AddConversationParticipant(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = parentUserId,
                JoinedAt = now,
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            });
            _repo.AddConversationParticipant(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = nannyUserId,
                JoinedAt = now,
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            });
        }

        var hiringOfferMessage = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderUserId = parentUserId,
            Content = "Đề nghị việc làm mới",
            MessageType = 4,
            AttachmentUrl = hiringRecord.Id.ToString(),
            CreatedAt = now,
            IsDeleted = false
        };
        _repo.AddMessage(hiringOfferMessage);
        conversation.LastMessageAt = now;

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = nannyUserId,
            Title = "Bạn nhận được đề nghị việc làm!",
            Content = $"{GetDisplayName(parentProfile.User)} muốn thuê bạn làm bảo mẫu.",
            Type = NotificationTypes.HiringOffer,
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

        await _communication.BroadcastPersistedMessageAsync(hiringOfferMessage.Id);

        return new HiringConfirmedDto
        {
            HiringRecordId = hiringRecord.Id,
            ContractId = contract.Id,
            ConversationId = conversation.Id,
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

        var template = await _repo.GetTemplateByIdAsync(dto.ContractTemplateId)
            ?? throw new KeyNotFoundException("Mẫu hợp đồng không tồn tại hoặc không hoạt động.");

        var now = DateTime.UtcNow;
        var contractNumber = await GenerateNextContractNumberAsync(now);

        var directJobPosting = new JobPosting
        {
            Id = Guid.NewGuid(),
            ParentProfileId = parentProfile.Id,
            Title = "Thuê bảo mẫu trực tiếp",
            Description = string.IsNullOrWhiteSpace(request.Message) ? "Tạo từ yêu cầu liên hệ đã được chấp nhận." : request.Message.Trim(),
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
            ContractTemplateId = template.Id,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ContractDuration = dto.ContractDuration,
            Status = 0,
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
            ContractTemplateId = template.Id,
            ContractContent = RenderTemplate(template.Content, parentProfile, nannyProfile, dto, directJobApplication, contractNumber),
            SignedByParent = true,
            SignedByNanny = false,
            Status = 0,
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        };
        _repo.AddContract(contract);

        var nannyUserId = nannyProfile.UserId;
        var conversation = await _repo.FindOneToOneConversationAsync(parentUserId, nannyUserId);
        if (conversation == null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = 1,
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            };
            _repo.AddConversation(conversation);

            _repo.AddConversationParticipant(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = parentUserId,
                JoinedAt = now,
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            });
            _repo.AddConversationParticipant(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                UserId = nannyUserId,
                JoinedAt = now,
                CreatedAt = now,
                CreatedBy = parentUserId,
                IsDeleted = false
            });
        }

        var hiringOfferMessageContact = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderUserId = parentUserId,
            Content = "Đề nghị việc làm mới",
            MessageType = 4,
            AttachmentUrl = hiringRecord.Id.ToString(),
            CreatedAt = now,
            IsDeleted = false
        };
        _repo.AddMessage(hiringOfferMessageContact);
        conversation.LastMessageAt = now;

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = nannyUserId,
            Title = "Bạn nhận được đề nghị việc làm!",
            Content = $"{GetDisplayName(parentProfile.User)} muốn thuê bạn làm bảo mẫu.",
            Type = NotificationTypes.HiringOffer,
            IsRead = false,
            RelatedEntityId = hiringRecord.Id,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        await _repo.SaveChangesAsync();

        await _communication.BroadcastPersistedMessageAsync(hiringOfferMessageContact.Id);

        return new HiringConfirmedDto
        {
            HiringRecordId = hiringRecord.Id,
            ContractId = contract.Id,
            ConversationId = conversation.Id,
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
        if (hiringRecord.Status != 0 || hiringRecord.NannyConfirmedAt.HasValue)
            throw new InvalidOperationException("Đề nghị này đã được xử lý trước đó.");

        var contract = await _repo.GetContractByHiringRecordIdAsync(hiringRecordId)
            ?? throw new InvalidOperationException("Không tìm thấy hợp đồng liên quan.");

        var now = DateTime.UtcNow;
        var parentUserId = hiringRecord.ParentProfile?.UserId ?? Guid.Empty;
        var jobApp = hiringRecord.JobApplication;
        var jobPosting = jobApp?.JobPosting;

        if (dto.Action.Equals("accept", StringComparison.OrdinalIgnoreCase))
        {
            // Keep hiring in pending until Parent finishes the final contract confirmation step.
            hiringRecord.Status = 0;
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
                    Content = $"{GetDisplayName(hiringRecord.NannyProfile?.User)} đã chấp nhận đề nghị. Vui lòng vào chi tiết hợp đồng để xác nhận cuối cùng.",
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
        else if (dto.Action.Equals("decline", StringComparison.OrdinalIgnoreCase))
        {
            hiringRecord.Status = 2;

            if (jobApp != null)
            {
                jobApp.Status = 4;
                jobApp.RejectionReason = string.IsNullOrWhiteSpace(dto.DeclineReason) ? "Bảo mẫu đã từ chối đề nghị." : dto.DeclineReason.Trim();
                jobApp.UpdatedAt = now;
                jobApp.UpdatedBy = nannyUserId;
            }

            if (jobPosting != null)
            {
                jobPosting.Status = 1;
                jobPosting.ClosedAt = null;
                jobPosting.UpdatedAt = now;
                jobPosting.UpdatedBy = nannyUserId;
            }

            if (parentUserId != Guid.Empty)
            {
                _repo.AddNotification(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = parentUserId,
                    Title = "Bảo mẫu đã từ chối đề nghị",
                    Content = $"{GetDisplayName(hiringRecord.NannyProfile?.User)} đã từ chối. Vị trí đã được mở lại.",
                    Type = NotificationTypes.HiringDeclined,
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
            throw new ArgumentException("Hành động không hợp lệ. Vui lòng chọn 'accept' hoặc 'decline'.");
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

    public async Task<List<object>> GetTemplatesForHiringAsync()
    {
        var templates = await _repo.GetTemplatesForHiringAsync();
        return templates.Select(t => (object)new
        {
            t.Id,
            t.Name,
            t.Version,
            t.IsActive
        }).ToList();
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

    private static string RenderTemplate(
        string template,
        ParentProfile parent,
        NannyProfile nanny,
        ConfirmHiringDto dto,
        JobApplication? app = null,
        string? contractNumber = null)
    {
        var posting = app?.JobPosting;
        var resolvedContractNumber = string.IsNullOrWhiteSpace(contractNumber)
            ? $"01/{DateTime.UtcNow.Year}/HĐLĐ-TGTG"
            : contractNumber.Trim();

        var content = template
            .Replace("{{ContractNumber}}", resolvedContractNumber)
            .Replace("{{ParentAddress}}", parent.User?.Address ?? string.Empty)
            .Replace("{{StartDate}}", dto.StartDate.ToString("dd/MM/yyyy"))
            .Replace("{{EndDate}}", dto.EndDate?.ToString("dd/MM/yyyy") ?? "Không xác định")
            .Replace("{{ContractDuration}}", dto.ContractDuration?.ToString() ?? string.Empty)
            .Replace("{{ContractDurationMonths}}", dto.ContractDuration?.ToString() ?? string.Empty)
            .Replace("{{JobTitle}}", posting?.Title ?? string.Empty)
            .Replace("{{JobDescription}}", posting?.Description?.Trim() ?? string.Empty)
            .Replace("{{SalaryMin}}", FormatMoney(posting?.SalaryMin))
            .Replace("{{SalaryMax}}", FormatMoney(posting?.SalaryMax))
            .Replace("{{SalaryType}}", MapSalaryType(posting?.SalaryType))
            .Replace("{{NumberOfChildren}}", posting?.NumberOfChildren?.ToString() ?? string.Empty)
            .Replace("{{NannyAddress}}", nanny.User?.Address ?? string.Empty)
            .Replace("{{CreatedDate}}", DateTime.UtcNow.ToString("dd/MM/yyyy"));

        content = ApplyWorkingTimeSection(content, posting?.JobScheduleRequirements);
        return ReplaceContractNumberLine(content, resolvedContractNumber);
    }

    private async Task<string> GenerateNextContractNumberAsync(DateTime nowUtc)
    {
        var year = nowUtc.Year;
        var existingCount = await _repo.CountContractsCreatedInYearAsync(year);
        var nextIndex = existingCount + 1;
        var serial = nextIndex < 100 ? nextIndex.ToString("00") : nextIndex.ToString();

        return $"{serial}/{year}/HĐLĐ-TGTG";
    }

    private static string ReplaceContractNumberLine(string content, string contractNumber)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        const string pattern = @"(?im)^\s*(Số|So)\s*:\s*.*$";
        if (!Regex.IsMatch(content, pattern))
            return content;

        return Regex.Replace(content, pattern, $"Số: {contractNumber}");
    }

    private static string ApplyWorkingTimeSection(string content, ICollection<JobScheduleRequirement>? requirements)
    {
        if (string.IsNullOrWhiteSpace(content) || requirements == null || requirements.Count == 0)
            return content;

        var slots = requirements
            .Where(s => !s.IsDeleted && s.IsRequired && s.DayOfWeek >= 0 && s.DayOfWeek <= 6 && s.TimeSlot >= 0 && s.TimeSlot <= 3)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.TimeSlot)
            .ToList();
        if (slots.Count == 0)
            return content;

        var dayNames = new[] { "Thu 2", "Thu 3", "Thu 4", "Thu 5", "Thu 6", "Thu 7", "Chu nhat" };
        var slotNames = new[] { "Sang", "Chieu", "Toi", "Dem" };
        var lines = slots
            .GroupBy(s => s.DayOfWeek)
            .Select(group =>
            {
                var slotText = string.Join(", ", group.Select(item => slotNames[item.TimeSlot]));
                return $"- {dayNames[group.Key]}: {slotText}";
            })
            .ToList();
        var sectionBody = string.Join(Environment.NewLine, lines);

        const string sectionPattern = @"(?is)(4\.1\.[^\r\n]*\r?\n)(.*?)(\r?\n\s*4\.2\.)";
        if (Regex.IsMatch(content, sectionPattern))
        {
            return Regex.Replace(
                content,
                sectionPattern,
                match => $"{match.Groups[1].Value}{sectionBody}{match.Groups[3].Value}");
        }

        const string section41Pattern = @"(?im)^(?<head>\s*4\.1\.[^\r\n]*)(?<tail>\r?\n|$)";
        if (Regex.IsMatch(content, section41Pattern))
        {
            return Regex.Replace(
                content,
                section41Pattern,
                match => $"{match.Groups["head"].Value}{Environment.NewLine}{sectionBody}{match.Groups["tail"].Value}");
        }

        return content;
    }

    private static string MapSalaryType(int? salaryType) =>
        salaryType switch
        {
            1 => "Theo gio",
            2 => "Theo thang",
            _ => string.Empty
        };

    private static string FormatMoney(decimal? amount)
    {
        if (!amount.HasValue) return string.Empty;
        return $"{amount.Value:0,0} VND";
    }

    private static string GetDisplayName(User? user)
    {
        if (user == null) return "Nguoi dung";
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
