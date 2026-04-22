using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class SearchService : ISearchService
{
    private readonly IJobApplicationRepository _jobAppRepo;
    private readonly INotificationService _notificationService;
    private readonly ISubscriptionService? _subscriptionService;

    public SearchService(
        IJobApplicationRepository jobAppRepo,
        INotificationService notificationService,
        ISubscriptionService? subscriptionService = null)
    {
        _jobAppRepo  = jobAppRepo;
        _notificationService = notificationService;
        _subscriptionService = subscriptionService;
    }

    public async Task<Guid?> GetNannyProfileIdByUserIdAsync(Guid userId) =>
        await _jobAppRepo.GetNannyProfileIdByUserIdAsync(userId);

    public async Task<ApplyToJobServiceResult> ApplyToJobAsync(Guid userId, Guid jobPostingId)
    {
        var nannyProfile = await _jobAppRepo.GetNannyProfileWithUserAsync(userId);
        if (nannyProfile == null)
        {
            return new ApplyToJobServiceResult(
                false, ApplyToJobFailure.NotNanny, null,
                default, default, default, default, false, "");
        }

        var job = await _jobAppRepo.GetJobPostingForApplyAsync(jobPostingId);
        if (job == null)
        {
            return new ApplyToJobServiceResult(
                false, ApplyToJobFailure.NotFound, null,
                default, default, default, default, false, "");
        }

        if (job.Status != (int)JobPostingStatus.Public ||
            job.ModerationStatus != (int)JobPostingModerationStatus.Approved)
        {
            return new ApplyToJobServiceResult(
                false, ApplyToJobFailure.JobNotOpen, null,
                default, default, default, default, false, "");
        }

        if (job.ParentProfile?.UserId == userId)
        {
            return new ApplyToJobServiceResult(
                false, ApplyToJobFailure.OwnJob, null,
                default, default, default, default, false, "");
        }

        var nowUtc = DateTime.UtcNow;
        var existingApplication = await _jobAppRepo.GetExistingApplicationAsync(jobPostingId, nannyProfile.Id);
        var monthlyApplicationLimit = await getMonthlyApplicationLimit(nannyProfile.Id);
        if (monthlyApplicationLimit > 0)
        {
            var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var appliedThisMonth = await _jobAppRepo.CountMonthlyApplicationsAsync(nannyProfile.Id, startOfMonth);
            var willConsumeMonthlyQuota = existingApplication == null || existingApplication.CreatedAt < startOfMonth;
            if (willConsumeMonthlyQuota && appliedThisMonth >= monthlyApplicationLimit)
            {
                return new ApplyToJobServiceResult(
                    false, ApplyToJobFailure.MonthlyLimit,
                    $"Ban da dat gioi han {monthlyApplicationLimit} luot ung tuyen trong thang nay. Vui long nang cap goi de ung tuyen them.",
                    default, default, default, default, false, "");
            }
        }

        JobApplication application;
        var isReapplied = false;
        if (existingApplication != null)
        {
            if (existingApplication.Status != 3)
            {
                return new ApplyToJobServiceResult(
                    false, ApplyToJobFailure.AlreadyApplied, null,
                    default, default, default, default, false, "");
            }

            existingApplication.Status = 0;
            existingApplication.WithdrawnAt = null;
            existingApplication.ReviewedAt = null;
            existingApplication.RejectionReason = null;
            if (existingApplication.CreatedAt.Year != nowUtc.Year || existingApplication.CreatedAt.Month != nowUtc.Month)
                existingApplication.CreatedAt = nowUtc;
            existingApplication.UpdatedAt   = nowUtc;
            existingApplication.UpdatedBy   = userId;
            application = existingApplication;
            isReapplied   = true;
        }
        else
        {
            application = new JobApplication
            {
                Id = Guid.NewGuid(),
                JobPostingId = jobPostingId,
                NannyProfileId = nannyProfile.Id,
                Status = 0,
                RejectionReason = null,
                ReviewedAt = null,
                WithdrawnAt = null,
                CreatedAt = nowUtc,
                CreatedBy = userId,
                UpdatedAt = null,
                UpdatedBy = null,
                IsDeleted = false
            };
            _jobAppRepo.AddApplication(application);
        }

        await _jobAppRepo.SaveChangesAsync();

        var nannyName = $"{nannyProfile.User?.FirstName} {nannyProfile.User?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(nannyName)) nannyName = "Mot nanny";

        var parentUserId = job.ParentProfile?.UserId ?? Guid.Empty;
        if (parentUserId != Guid.Empty)
        {
            await _notificationService.createNotification(
                parentUserId,
                "Co nanny vua ung tuyen bai dang cua ban",
                $"{nannyName} vua gui don ung tuyen cho bai dang \"{job.Title}\".",
                NotificationTypes.JobApplicationReceived,
                job.Id,
                "JobPosting",
                userId);
        }

        await _notificationService.createNotification(
            userId,
            "Ban da gui don ung tuyen",
            $"Don ung tuyen cua ban cho bai dang \"{job.Title}\" da duoc gui. Vui long cho Parent phan hoi.",
            NotificationTypes.JobApplicationSubmitted,
            application.Id,
            "JobApplication",
            userId);

        var successMsg = isReapplied
            ? "Ban da gui lai don ung tuyen. Vui long cho Parent phan hoi."
            : "Ban da ung tuyen thanh cong. Vui long cho Parent phan hoi.";

        return new ApplyToJobServiceResult(
            true, null, null,
            application.Id, jobPostingId, parentUserId, userId, isReapplied, successMsg);
    }

    public async Task<NannyMyApplicationsListResult?> GetMyApplicationsAsync(
        Guid userId, int page, int pageSize)
    {
        var nannyProfile = await _jobAppRepo.GetNannyProfileWithUserAsync(userId);
        if (nannyProfile == null) return null;

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 50);
        var skip = (page - 1) * pageSize;

        var (items, total) = await _jobAppRepo.GetPagedApplicationsForNannyAsync(
            nannyProfile.Id, skip, pageSize);

        var data = items.Select(a => new NannyMyApplicationItemDto(
            a.Id,
            a.JobPostingId,
            a.JobPosting?.Title ?? "Tin dang",
            $"{a.JobPosting?.ParentProfile?.User?.FirstName} {a.JobPosting?.ParentProfile?.User?.LastName}".Trim(),
            a.JobPosting?.City,
            a.JobPosting?.District,
            a.JobPosting?.Location,
            a.Status,
            getApplicationStatusLabel(a.Status),
            a.Status == 0,
            a.CreatedAt,
            a.ReviewedAt,
            a.WithdrawnAt
        )).ToList();

        return new NannyMyApplicationsListResult(data, total, page, pageSize);
    }

    public async Task<WithdrawApplicationFailureResult> WithdrawApplicationAsync(
        Guid userId, Guid applicationId)
    {
        var nannyProfileId = await _jobAppRepo.GetNannyProfileIdByUserIdAsync(userId);
        if (!nannyProfileId.HasValue)
        {
            return new WithdrawApplicationFailureResult(false, WithdrawForNannyFailure.NotNanny, null);
        }

        var application = await _jobAppRepo.GetApplicationForWithdrawAsync(
            applicationId, nannyProfileId.Value);
        if (application == null)
        {
            return new WithdrawApplicationFailureResult(false, WithdrawForNannyFailure.NotFound, null);
        }

        if (application.Status != 0)
        {
            return new WithdrawApplicationFailureResult(false, WithdrawForNannyFailure.NotPending, null);
        }

        var nowUtc = DateTime.UtcNow;
        application.Status     = 3;
        application.WithdrawnAt = nowUtc;
        application.UpdatedAt   = nowUtc;
        application.UpdatedBy   = userId;

        await _jobAppRepo.SaveChangesAsync();
        return new WithdrawApplicationFailureResult(true, null, null);
    }

    public async Task<(GetParentJobApplicationsFailure? Error, string? ErrorMessage, ParentJobApplicationsListResult? Data)>
        GetJobApplicationsForParentAsync(Guid userId, Guid jobPostingId, int? status)
    {
        if (status.HasValue && (status.Value < 0 || status.Value > 3))
        {
            return (GetParentJobApplicationsFailure.InvalidStatusFilter, "Trang thai don ung tuyen khong hop le.", null);
        }

        var parentProfileId = await _jobAppRepo.GetParentProfileIdByUserIdAsync(userId);
        if (!parentProfileId.HasValue)
        {
            return (GetParentJobApplicationsFailure.NotParent, null, null);
        }

        var job = await _jobAppRepo.GetJobPostingForParentAsync(jobPostingId, parentProfileId.Value);
        if (job == null)
        {
            return (GetParentJobApplicationsFailure.JobNotFound, "Khong tim thay bai dang hoac ban khong co quyen truy cap.", null);
        }

        var applications = await _jobAppRepo.GetApplicationsByJobPostingAsync(jobPostingId, status);

        var rows = new List<ParentApplicationRowDto>();
        foreach (var a in applications)
        {
            var nannyUser = a.NannyProfile?.User;
            var nannyName = $"{nannyUser?.FirstName} {nannyUser?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(nannyName)) nannyName = "Nanny";

            rows.Add(new ParentApplicationRowDto(
                a.Id,
                a.Status,
                getApplicationStatusLabel(a.Status),
                a.CreatedAt,
                a.ReviewedAt,
                a.WithdrawnAt,
                a.RejectionReason,
                a.Status == 0,
                new ParentNannyInApplicationDto(
                    a.NannyProfileId,
                    nannyUser?.Id,
                    nannyName,
                    nannyUser?.AvatarUrl,
                    nannyUser?.PhoneNumber,
                    nannyUser?.City,
                    nannyUser?.District,
                    a.NannyProfile?.YearsOfExperience,
                    a.NannyProfile?.ExpectedSalaryMin,
                    a.NannyProfile?.ExpectedSalaryMax
                )));
        }

        var jobDto = new ParentJobSummaryDto(
            job.Id, job.Title, job.Status, job.ModerationStatus,
            job.City, job.District, job.Location,
            job.CreatedAt, job.PublishedAt, job.ExpiresAt);

        var bundle = new ParentJobApplicationsListResult(
            jobDto,
            rows.Count,
            rows.Count(r => r.Status == 0),
            rows);

        return (null, null, bundle);
    }

    public async Task<ReviewJobApplicationServiceResult> ReviewJobApplicationAsync(
        Guid userId, Guid applicationId, ReviewJobApplicationRequestDto? request)
    {
        if (request == null)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.BadInput, "Du lieu review khong hop le.",
                default, default, default, default, default, default, default, default, "");
        }

        if (request.Action is not 1 and not 2)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.BadInput, "Action khong hop le. Dung 1 (accept) hoac 2 (reject).",
                default, default, default, default, default, default, default, default, "");
        }

        if (request.Action == 2 && string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.BadInput, "Vui long nhap ly do khi tu choi request.",
                default, default, default, default, default, default, default, default, "");
        }

        var parentProfileId = await _jobAppRepo.GetParentProfileIdByUserIdAsync(userId);
        if (!parentProfileId.HasValue)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.NotParent, null,
                default, default, default, default, default, default, default, default, "");
        }

        var application = await _jobAppRepo.GetApplicationForReviewAsync(applicationId);
        if (application == null)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.ApplicationNotFound, "Khong tim thay request ung tuyen.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.JobPosting == null || application.JobPosting.IsDeleted ||
            application.JobPosting.ParentProfileId != parentProfileId.Value)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.Forbidden, "Khong tim thay request ung tuyen hoac ban khong co quyen xu ly.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.Status == 3)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.NannyWithdrawn, "Request nay da duoc nanny huy.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.Status is 1 or 2)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.AlreadyProcessed, "Request nay da duoc xu ly truoc do.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.Status != 0)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.NotPending, "Chi request dang cho duyet moi co the xu ly.",
                default, default, default, default, default, default, default, default, "");
        }

        var nowUtc = DateTime.UtcNow;
        var isApproved = request.Action == 1;
        application.Status = isApproved ? 1 : 2;
        application.ReviewedAt = nowUtc;
        application.WithdrawnAt = null;
        application.RejectionReason = isApproved ? null : request.RejectionReason?.Trim();
        application.UpdatedAt = nowUtc;
        application.UpdatedBy = userId;

        await _jobAppRepo.SaveChangesAsync();

        var nannyUserId = application.NannyProfile?.UserId ?? Guid.Empty;
        if (nannyUserId != Guid.Empty && !isApproved)
        {
            var title = "Don ung tuyen bi tu choi";
            var content =
                $"Parent da tu choi don ung tuyen cua ban cho bai dang \"{application.JobPosting.Title}\". Ly do: {application.RejectionReason}";

            await _notificationService.createNotification(
                nannyUserId,
                title,
                content,
                NotificationTypes.JobApplicationRejected,
                application.Id,
                "JobApplication",
                userId);
        }

        return new ReviewJobApplicationServiceResult(
            true, null, null,
            application.Id,
            application.JobPostingId,
            application.Status,
            getApplicationStatusLabel(application.Status),
            application.ReviewedAt,
            application.RejectionReason,
            userId,
            nannyUserId,
            isApproved
                ? "Ban da chap nhan request ung tuyen."
                : "Ban da tu choi request ung tuyen.");
    }

    private async Task<int> getMonthlyApplicationLimit(Guid nannyProfileId)
    {
        if (_subscriptionService == null)
            return SubscriptionBenefitResponse.FreeNanny.MonthlyApplicationLimit;

        var benefits = await _subscriptionService.getBenefitsForNannyProfile(nannyProfileId);
        return benefits.MonthlyApplicationLimit;
    }

    private static string getApplicationStatusLabel(int status) =>
        status switch
        {
            0 => "Dang cho duyet",
            1 => "Da duoc chap nhan",
            2 => "Da bi tu choi",
            3 => "Da huy",
            _ => "Dang cap nhat"
        };
}
