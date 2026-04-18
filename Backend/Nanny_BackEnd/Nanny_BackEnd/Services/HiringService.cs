using Nanny_BackEnd.DTOs.Hiring;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class HiringService
{
    private readonly HiringRepository _repo;

    public HiringService(HiringRepository repo) => _repo = repo;

    public async Task<List<JobApplicantDto>> GetApplicantsAsync(Guid jobPostingId, Guid parentUserId)
    {
        var job = await _repo.GetJobPostingByIdAsync(jobPostingId)
            ?? throw new KeyNotFoundException("Khong tim thay bai dang.");

        if (job.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Ban khong co quyen xem danh sach ung vien cua bai dang nay.");

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
            throw new InvalidOperationException("Ung vien nay da duoc xu ly, khong the dong y nua.");

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
            ?? throw new KeyNotFoundException("Khong tim thay don ung tuyen.");

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
            throw new InvalidOperationException("Ung vien nay chua duoc dong y truoc khi thue.");

        var latestHiring = await _repo.GetLatestHiringRecordByJobApplicationIdAsync(jobAppId);
        if (latestHiring != null && latestHiring.Status is 0 or 1 or 3)
            throw new InvalidOperationException("Ung vien nay dang co offer hoac hop dong dang hieu luc.");

        var template = await _repo.GetTemplateByIdAsync(dto.ContractTemplateId)
            ?? throw new KeyNotFoundException("Mau hop dong khong ton tai hoac khong hoat dong.");

        var parentProfile = await _repo.GetParentProfileByUserIdAsync(parentUserId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so phu huynh.");

        var nannyProfile = app.NannyProfile
            ?? throw new InvalidOperationException("Khong tim thay thong tin nanny.");

        var now = DateTime.UtcNow;

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
            ContractContent = RenderTemplate(template.Content, parentProfile, nannyProfile, dto, app),
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

        _repo.AddMessage(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderUserId = parentUserId,
            Content = "De nghi viec lam moi",
            MessageType = 4,
            AttachmentUrl = hiringRecord.Id.ToString(),
            CreatedAt = now,
            IsDeleted = false
        });
        conversation.LastMessageAt = now;

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = nannyUserId,
            Title = "Ban nhan duoc de nghi viec lam!",
            Content = $"{GetDisplayName(parentProfile.User)} muon thue ban lam bao mau.",
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
                Title = "Don ung tuyen khong duoc chon",
                Content = "Vi tri da duoc tuyen dung boi ung vien khac.",
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
            ?? throw new KeyNotFoundException("Khong tim thay request contact da duoc chap nhan.");

        if (request.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Ban khong co quyen tao hiring record tu request nay.");

        var parentProfile = request.ParentProfile
            ?? throw new InvalidOperationException("Khong tim thay ho so phu huynh.");
        var nannyProfile = request.NannyProfile
            ?? throw new InvalidOperationException("Khong tim thay ho so nanny.");

        var template = await _repo.GetTemplateByIdAsync(dto.ContractTemplateId)
            ?? throw new KeyNotFoundException("Mau hop dong khong ton tai hoac khong hoat dong.");

        var now = DateTime.UtcNow;

        var directJobPosting = new JobPosting
        {
            Id = Guid.NewGuid(),
            ParentProfileId = parentProfile.Id,
            Title = "Thue nanny truc tiep",
            Description = string.IsNullOrWhiteSpace(request.Message) ? "Hiring tao tu luong contact request da duoc chap nhan." : request.Message.Trim(),
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
            ContractContent = RenderTemplate(template.Content, parentProfile, nannyProfile, dto, directJobApplication),
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

        _repo.AddMessage(new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderUserId = parentUserId,
            Content = "De nghi viec lam moi",
            MessageType = 4,
            AttachmentUrl = hiringRecord.Id.ToString(),
            CreatedAt = now,
            IsDeleted = false
        });
        conversation.LastMessageAt = now;

        _repo.AddNotification(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = nannyUserId,
            Title = "Ban nhan duoc de nghi viec lam!",
            Content = $"{GetDisplayName(parentProfile.User)} muon thue ban lam bao mau.",
            Type = NotificationTypes.HiringOffer,
            IsRead = false,
            RelatedEntityId = hiringRecord.Id,
            RelatedEntityType = "HiringRecord",
            CreatedAt = now,
            CreatedBy = parentUserId,
            IsDeleted = false
        });

        await _repo.SaveChangesAsync();

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
            ?? throw new KeyNotFoundException("Khong tim thay thong tin tuyen dung.");

        var parentUserId = hiringRecord.ParentProfile?.UserId;
        var nannyUserId = hiringRecord.NannyProfile?.UserId;
        if (currentUserId != parentUserId && currentUserId != nannyUserId)
            throw new UnauthorizedAccessException("Ban khong co quyen xem thong tin tuyen dung nay.");

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
            ?? throw new KeyNotFoundException("Khong tim thay thong tin tuyen dung.");

        if (hiringRecord.NannyProfile?.UserId != nannyUserId)
            throw new UnauthorizedAccessException("Ban khong co quyen phan hoi offer nay.");
        if (hiringRecord.Status != 0)
            throw new InvalidOperationException("Offer nay da duoc xu ly truoc do.");

        var contract = await _repo.GetContractByHiringRecordIdAsync(hiringRecordId)
            ?? throw new InvalidOperationException("Khong tim thay hop dong lien quan.");

        var now = DateTime.UtcNow;
        var parentUserId = hiringRecord.ParentProfile?.UserId ?? Guid.Empty;
        var jobApp = hiringRecord.JobApplication;
        var jobPosting = jobApp?.JobPosting;

        if (dto.Action.Equals("accept", StringComparison.OrdinalIgnoreCase))
        {
            hiringRecord.Status = 1;
            hiringRecord.NannyConfirmedAt = now;

            contract.SignedByNanny = true;
            contract.SignedAt = now;
            contract.Status = 1;

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
                    pendingApplication.RejectionReason = "Vi tri da duoc nanny khac chap nhan offer.";
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
                    Title = "Nanny da chap nhan de nghi!",
                    Content = $"{GetDisplayName(hiringRecord.NannyProfile?.User)} da chap nhan lam viec cho ban.",
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
                jobApp.RejectionReason = string.IsNullOrWhiteSpace(dto.DeclineReason) ? "Nanny da tu choi offer." : dto.DeclineReason.Trim();
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
                    Title = "Nanny da tu choi de nghi",
                    Content = $"{GetDisplayName(hiringRecord.NannyProfile?.User)} da tu choi. Vi tri da duoc mo lai.",
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
            throw new ArgumentException("Hanh dong khong hop le. Vui long chon 'accept' hoac 'decline'.");
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
            ?? throw new KeyNotFoundException("Khong tim thay hop dong.");

        if (hiringRecord.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Ban khong co quyen hoan thanh hop dong nay.");

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
                Title = "Hop dong da hoan thanh",
                Content = $"{GetDisplayName(hiringRecord.ParentProfile?.User)} da xac nhan hop dong hoan thanh.",
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

    public async Task<List<object>> GetActiveTemplatesAsync()
    {
        var templates = await _repo.GetActiveTemplatesAsync();
        return templates.Select(t => (object)new
        {
            t.Id,
            t.Name,
            t.Version
        }).ToList();
    }

    private async Task<JobApplication> GetVerifiedApplicationAsync(Guid jobPostingId, Guid jobAppId, Guid parentUserId)
    {
        var app = await _repo.GetJobApplicationByIdAsync(jobAppId)
            ?? throw new KeyNotFoundException("Khong tim thay don ung tuyen.");

        if (app.JobPostingId != jobPostingId)
            throw new ArgumentException("Don ung tuyen khong thuoc bai dang nay.");
        if (app.JobPosting?.ParentProfile?.UserId != parentUserId)
            throw new UnauthorizedAccessException("Ban khong co quyen thuc hien hanh dong nay voi don ung tuyen nay.");

        return app;
    }

    private static void ValidateContractDates(ConfirmHiringDto dto)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (dto.StartDate < today)
            throw new ArgumentException("Ngay bat dau khong duoc truoc ngay tao hop dong.");

        if (dto.EndDate.HasValue && dto.EndDate.Value <= dto.StartDate)
            throw new ArgumentException("Ngay ket thuc phai lon hon ngay bat dau.");
    }

    private static string RenderTemplate(
        string template,
        ParentProfile parent,
        NannyProfile nanny,
        ConfirmHiringDto dto,
        JobApplication? app = null)
    {
        var posting = app?.JobPosting;

        return template
            .Replace("{{ParentName}}", GetDisplayName(parent.User))
            .Replace("{{NannyName}}", GetDisplayName(nanny.User))
            .Replace("{{ParentPhone}}", parent.User?.PhoneNumber ?? string.Empty)
            .Replace("{{NannyPhone}}", nanny.User?.PhoneNumber ?? string.Empty)
            .Replace("{{ParentAddress}}", parent.User?.Address ?? string.Empty)
            .Replace("{{StartDate}}", dto.StartDate.ToString("dd/MM/yyyy"))
            .Replace("{{EndDate}}", dto.EndDate?.ToString("dd/MM/yyyy") ?? "Khong xac dinh")
            .Replace("{{ContractDuration}}", dto.ContractDuration?.ToString() ?? string.Empty)
            .Replace("{{JobTitle}}", posting?.Title ?? string.Empty)
            .Replace("{{SalaryMin}}", FormatMoney(posting?.SalaryMin))
            .Replace("{{SalaryMax}}", FormatMoney(posting?.SalaryMax))
            .Replace("{{SalaryType}}", MapSalaryType(posting?.SalaryType))
            .Replace("{{NumberOfChildren}}", posting?.NumberOfChildren?.ToString() ?? string.Empty)
            .Replace("{{NannyAddress}}", nanny.User?.Address ?? string.Empty)
            .Replace("{{NannyEmail}}", nanny.User?.Email ?? string.Empty)
            .Replace("{{ParentEmail}}", parent.User?.Email ?? string.Empty)
            .Replace("{{CreatedDate}}", DateTime.UtcNow.ToString("dd/MM/yyyy"));
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
