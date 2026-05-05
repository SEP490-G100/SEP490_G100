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

        var nowUtc = DateTime.UtcNow;
        var isIdentityVerified = nannyProfile.VerificationStatus == (int)VerificationStatus.Approved;
        var hasApprovedHealthCertificate =
            await _jobAppRepo.HasApprovedHealthCertificateAsync(nannyProfile.Id, nowUtc);

        if (!isIdentityVerified || !hasApprovedHealthCertificate)
        {
            return new ApplyToJobServiceResult(
                false, ApplyToJobFailure.MissingRequiredVerifications, null,
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
                    $"Bạn đã đạt giới hạn {monthlyApplicationLimit} lượt ứng tuyển trong tháng này. Vui lòng nâng cấp gói để ứng tuyển thêm.",
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
        if (string.IsNullOrWhiteSpace(nannyName)) nannyName = "Một nanny";

        var parentUserId = job.ParentProfile?.UserId ?? Guid.Empty;
        if (parentUserId != Guid.Empty)
        {
            await _notificationService.createNotification(
                parentUserId,
                "Có nanny vừa ứng tuyển bài đăng của bạn",
                $"{nannyName} vừa gửi đơn ứng tuyển cho bài đăng \"{job.Title}\".",
                NotificationTypes.JobApplicationReceived,
                job.Id,
                "JobPosting",
                userId);
        }

        await _notificationService.createNotification(
            userId,
            "Bạn đã gửi đơn ứng tuyển",
            $"Đơn ứng tuyển của bạn cho bài đăng \"{job.Title}\" đã được gửi. Vui lòng chờ Parent phản hồi.",
            NotificationTypes.JobApplicationSubmitted,
            application.Id,
            "JobApplication",
            userId);

        var successMsg = isReapplied
            ? "Bạn đã gửi lại đơn ứng tuyển. Vui lòng chờ Parent phản hồi."
            : "Bạn đã ứng tuyển thành công. Vui lòng chờ Parent phản hồi.";

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
            return (GetParentJobApplicationsFailure.InvalidStatusFilter, "Trạng thái đơn ứng tuyển không hợp lệ.", null);
        }

        var parentProfileId = await _jobAppRepo.GetParentProfileIdByUserIdAsync(userId);
        if (!parentProfileId.HasValue)
        {
            return (GetParentJobApplicationsFailure.NotParent, null, null);
        }

        var job = await _jobAppRepo.GetJobPostingForParentAsync(jobPostingId, parentProfileId.Value);
        if (job == null)
        {
            return (GetParentJobApplicationsFailure.JobNotFound, "Không tìm thấy bài đăng hoặc bạn không có quyền truy cập.", null);
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
                false, ReviewJobParentFailure.BadInput, "Dữ liệu review không hợp lệ.",
                default, default, default, default, default, default, default, default, "");
        }

        if (request.Action is not 1 and not 2)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.BadInput, "Action không hợp lệ. Dùng 1 (accept) hoặc 2 (reject).",
                default, default, default, default, default, default, default, default, "");
        }

        if (request.Action == 2 && string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.BadInput, "Vui lòng nhập lý do khi từ chối request.",
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
                false, ReviewJobParentFailure.ApplicationNotFound, "Không tìm thấy request ứng tuyển.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.JobPosting == null || application.JobPosting.IsDeleted ||
            application.JobPosting.ParentProfileId != parentProfileId.Value)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.Forbidden, "Không tìm thấy request ứng tuyển hoặc bạn không có quyền xử lý.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.Status == 3)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.NannyWithdrawn, "Request này đã được nanny hủy.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.Status is 1 or 2)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.AlreadyProcessed, "Request này đã được xử lý trước đó.",
                default, default, default, default, default, default, default, default, "");
        }

        if (application.Status != 0)
        {
            return new ReviewJobApplicationServiceResult(
                false, ReviewJobParentFailure.NotPending, "Chỉ request đang chờ duyệt mới có thể xử lý.",
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
        if (nannyUserId != Guid.Empty)
        {
            var title = "Đơn ứng tuyển bị từ chối";
            var content =
                $"Parent đã từ chối đơn ứng tuyển của bạn cho bài đăng \"{application.JobPosting.Title}\". Lý do: {application.RejectionReason}";

            if (isApproved)
            {
                title = "Đơn ứng tuyển được chấp nhận";
                content = $"Parent đã chấp nhận đơn ứng tuyển của bạn cho bài đăng \"{application.JobPosting.Title}\".";
            }

            await _notificationService.createNotification(
                nannyUserId,
                title,
                content,
                isApproved ? NotificationTypes.JobApplicationApproved : NotificationTypes.JobApplicationRejected,
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
                ? "Bạn đã chấp nhận request ứng tuyển."
                : "Bạn đã từ chối request ứng tuyển.");
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
            0 => "Đang chờ duyệt",
            1 => "Đã được chấp nhận",
            2 => "Đã bị từ chối",
            3 => "Đã hủy",
            _ => "Đang cập nhật"
        };
}
