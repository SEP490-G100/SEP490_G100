using System.ComponentModel.DataAnnotations;
using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Exceptions;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class ReportService
{
    private static readonly TimeSpan HourlyWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan DailyWindow = TimeSpan.FromDays(1);
    private static readonly TimeSpan TargetCompletedCooldown = TimeSpan.FromHours(10);
    private const int MaxReportsPerHour = 3;
    private const int MaxReportsPerDay = 10;

    private readonly ReportRepository _reportRepo;
    private readonly UserRepository _userRepo;
    private readonly NotificationService _notificationService;

    public ReportService(
        ReportRepository reportRepo,
        UserRepository userRepo,
        NotificationService notificationService)
    {
        _reportRepo = reportRepo;
        _userRepo = userRepo;
        _notificationService = notificationService;
    }

    public async Task<Guid> ReportJobPostingAsync(Guid jobPostingId, Guid reporterUserId, CreateReportRequest request)
    {
        var job = await _reportRepo.GetJobPostingForReportAsync(jobPostingId)
            ?? throw new KeyNotFoundException("Không tìm thấy tin đăng hoặc tin đã bị xóa.");

        if (job.ParentProfile?.UserId == reporterUserId)
            throw new InvalidOperationException("Bạn không thể báo cáo bài đăng của chính mình.");

        var report = await createReportAsync(reporterUserId, jobPostingId, "JobPosting", request);

        var reporter = await _userRepo.FindByIdAsync(reporterUserId);
        await _notificationService.createNotificationForModerators(
            "Có báo cáo bài đăng mới",
            $"{getDisplayName(reporter)} vừa gửi báo cáo cho bài đăng \"{job.Title}\".",
            NotificationTypes.ReportSubmitted,
            report.Id,
            "Report",
            reporterUserId);

        return report.Id;
    }

    public async Task<Guid> ReportProfileAsync(Guid profileUserId, Guid reporterUserId, CreateReportRequest request)
    {
        var targetUser = await _reportRepo.GetUserForProfileReportAsync(profileUserId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ cần báo cáo.");

        if (targetUser.Id == reporterUserId)
            throw new InvalidOperationException("Bạn không thể báo cáo hồ sơ của chính mình.");

        var report = await createReportAsync(reporterUserId, targetUser.Id, "Profile", request);

        var reporter = await _userRepo.FindByIdAsync(reporterUserId);
        await _notificationService.createNotificationForModerators(
            "Có báo cáo hồ sơ mới",
            $"{getDisplayName(reporter)} vừa gửi báo cáo hồ sơ người dùng.",
            NotificationTypes.ReportSubmitted,
            report.Id,
            "Report",
            reporterUserId);

        return report.Id;
    }

    public async Task<ReportListResponse> GetModeratorReportsAsync(
        int? status,
        string? entityType,
        string? search,
        int page,
        int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, totalCount) = await _reportRepo.GetPagedReportsAsync(
            status, entityType, search, page, pageSize);

        return new ReportListResponse
        {
            Items = items.Select(MapListItem).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<(bool Success, ReportDetailDto? Data, string? Message)> GetModeratorReportDetailAsync(Guid id)
    {
        var report = await _reportRepo.GetReportByIdAsync(id, includeDeleted: true);
        if (report == null)
            return (false, null, "Report not found.");

        var detail = MapDetail(report);
        await EnrichReportDetailAsync(report, detail);
        return (true, detail, null);
    }

    public async Task<(bool Success, int StatusCode, string Message)> ResolveReportAsync(
        Guid id,
        Guid moderatorUserId,
        ResolveReportRequest request)
    {
        if (request == null)
            return (false, 400, "Request body is required.");

        var resolveValidation = validateResolveReportRequest(request);
        if (resolveValidation != null)
            return (false, 400, resolveValidation);

        var report = await _reportRepo.GetReportByIdAsync(id, includeDeleted: true);
        if (report == null)
            return (false, 404, "Report not found.");

        if (report.IsDeleted)
            return (false, 400, "Cannot resolve a deactivated report.");
        if (report.Status == 1)
            return (false, 400, "Complaint already completed. Resolution cannot be edited.");

        var resolution = request.Resolution?.Trim();
        var actionTaken = request.ActionTaken?.Trim();
        var offenderNotificationMessage = request.OffenderNotificationMessage?.Trim();

        if (string.IsNullOrWhiteSpace(resolution))
            return (false, 400, "Resolution is required.");
        if (string.IsNullOrWhiteSpace(actionTaken))
            return (false, 400, "ActionTaken is required.");

        report.Resolution = resolution;
        report.ActionTaken = actionTaken;
        report.Status = 1;
        report.HandledBy = moderatorUserId;
        report.HandledAt = DateTime.UtcNow;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = moderatorUserId;

        await _reportRepo.SaveChangesAsync();

        await _notificationService.createNotification(
            report.ReporterUserId,
            "Thong bao tu Moderator",
            "Chúng tôi đã xử lí yêu cầu phàn nàn của bạn",
            NotificationTypes.ModeratorBroadcast,
            report.Id,
            "Report",
            moderatorUserId);

        if (!string.IsNullOrWhiteSpace(offenderNotificationMessage))
        {
            var offenderUserId = await ResolveOffenderUserIdAsync(report);
            if (offenderUserId.HasValue)
            {
                await _notificationService.createNotification(
                    offenderUserId.Value,
                    "Thong bao tu Moderator",
                    offenderNotificationMessage,
                    NotificationTypes.ModeratorBroadcast,
                    report.Id,
                    "Report",
                    moderatorUserId);
            }
        }

        return (true, 200, "Report resolved successfully.");
    }

    public async Task<(bool Success, int StatusCode, string Message)> ToggleReportStatusAsync(
        Guid id,
        Guid moderatorUserId,
        bool isActive)
    {
        var report = await _reportRepo.GetReportByIdAsync(id, includeDeleted: true);
        if (report == null)
            return (false, 404, "Report not found.");

        report.IsDeleted = !isActive;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = moderatorUserId;

        await _reportRepo.SaveChangesAsync();

        return (true, 200, isActive
            ? "Report activated successfully."
            : "Report deactivated successfully.");
    }

    private async Task<Report> createReportAsync(
        Guid reporterUserId,
        Guid reportedEntityId,
        string reportedEntityType,
        CreateReportRequest request)
    {
        if (request == null)
            throw new InvalidOperationException("Request body is required.");

        var createValidation = validateCreateReportRequest(request);
        if (!string.IsNullOrWhiteSpace(createValidation))
            throw new InvalidOperationException(createValidation);

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Lý do báo cáo là bắt buộc.");

        var hasPending = await _reportRepo.HasPendingReportAsync(reporterUserId, reportedEntityId, reportedEntityType);
        if (hasPending)
            throw new InvalidOperationException("Bạn đã gửi báo cáo này và đang chờ xử lý.");

        var nowUtc = DateTime.UtcNow;

        await ensureTargetCooldownAsync(reporterUserId, reportedEntityId, reportedEntityType, nowUtc);
        await ensureGlobalRateLimitsAsync(reporterUserId, nowUtc);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = reporterUserId,
            ReportedEntityId = reportedEntityId,
            ReportedEntityType = reportedEntityType,
            Reason = reason,
            Evidence = string.IsNullOrWhiteSpace(request.Evidence) ? null : request.Evidence.Trim(),
            Status = 0,
            CreatedAt = nowUtc,
            CreatedBy = reporterUserId,
            IsDeleted = false
        };

        _reportRepo.AddReport(report);
        await _reportRepo.SaveChangesAsync();
        return report;
    }

    private async Task ensureTargetCooldownAsync(
        Guid reporterUserId,
        Guid reportedEntityId,
        string reportedEntityType,
        DateTime nowUtc)
    {
        var latestCompletedAt = await _reportRepo.GetLatestCompletedReportMomentAsync(
            reporterUserId,
            reportedEntityId,
            reportedEntityType);

        if (!latestCompletedAt.HasValue)
            return;

        var cooldownUntil = latestCompletedAt.Value.Add(TargetCompletedCooldown);
        if (cooldownUntil <= nowUtc)
            return;

        throw new RateLimitExceededException(
            "REPORT_TARGET_COOLDOWN",
            "Bạn vừa báo cáo đối tượng này. Vui lòng thử lại sau 10 giờ.",
            cooldownUntil);
    }

    private async Task ensureGlobalRateLimitsAsync(Guid reporterUserId, DateTime nowUtc)
    {
        var hourWindowStart = nowUtc.Subtract(HourlyWindow);
        var hourlyCount = await _reportRepo.CountReportsSinceAsync(reporterUserId, hourWindowStart);
        if (hourlyCount >= MaxReportsPerHour)
        {
            var oldestInHour = await _reportRepo.GetOldestReportCreatedAtSinceAsync(reporterUserId, hourWindowStart);
            var cooldownUntil = (oldestInHour ?? nowUtc).Add(HourlyWindow);

            throw new RateLimitExceededException(
                "REPORT_RATE_LIMIT_HOURLY",
                "Bạn đã vượt quá giới hạn 3 báo cáo trong 1 giờ. Vui lòng thử lại sau.",
                cooldownUntil);
        }

        var dayWindowStart = nowUtc.Subtract(DailyWindow);
        var dailyCount = await _reportRepo.CountReportsSinceAsync(reporterUserId, dayWindowStart);
        if (dailyCount >= MaxReportsPerDay)
        {
            var oldestInDay = await _reportRepo.GetOldestReportCreatedAtSinceAsync(reporterUserId, dayWindowStart);
            var cooldownUntil = (oldestInDay ?? nowUtc).Add(DailyWindow);

            throw new RateLimitExceededException(
                "REPORT_RATE_LIMIT_DAILY",
                "Bạn đã vượt quá giới hạn 10 báo cáo trong 24 giờ. Vui lòng thử lại sau.",
                cooldownUntil);
        }
    }

    private static string getDisplayName(User? user)
    {
        if (user == null) return "Nguoi dung";
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static ReportListItemDto MapListItem(Report report)
    {
        return new ReportListItemDto
        {
            Id = report.Id,
            ReporterUserId = report.ReporterUserId,
            ReporterName = getDisplayName(report.ReporterUser),
            ReporterEmail = report.ReporterUser?.Email ?? string.Empty,
            ReportedEntityId = report.ReportedEntityId,
            ReportedEntityType = report.ReportedEntityType,
            Reason = report.Reason,
            Evidence = report.Evidence,
            Status = report.Status,
            HandledBy = report.HandledBy,
            HandledByName = getDisplayName(report.HandledByNavigation),
            HandledAt = report.HandledAt,
            Resolution = report.Resolution,
            ActionTaken = report.ActionTaken,
            CreatedAt = report.CreatedAt,
            IsDeleted = report.IsDeleted
        };
    }

    private static ReportDetailDto MapDetail(Report report)
    {
        return new ReportDetailDto
        {
            Id = report.Id,
            ReporterUserId = report.ReporterUserId,
            ReporterName = getDisplayName(report.ReporterUser),
            ReporterEmail = report.ReporterUser?.Email ?? string.Empty,
            ReportedEntityId = report.ReportedEntityId,
            ReportedEntityType = report.ReportedEntityType,
            Reason = report.Reason,
            Evidence = report.Evidence,
            Status = report.Status,
            HandledBy = report.HandledBy,
            HandledByName = getDisplayName(report.HandledByNavigation),
            HandledAt = report.HandledAt,
            Resolution = report.Resolution,
            ActionTaken = report.ActionTaken,
            OffenderUserId = null,
            OffenderName = null,
            OffenderEmail = null,
            ConversationId = null,
            ReportedMessageContent = null,
            JobPostingTitle = null,
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt,
            IsDeleted = report.IsDeleted
        };
    }

    private async Task EnrichReportDetailAsync(Report report, ReportDetailDto detail)
    {
        if (report.ReportedEntityType.Equals("Message", StringComparison.OrdinalIgnoreCase))
        {
            var message = await _reportRepo.GetMessageDetailForModeratorAsync(report.ReportedEntityId);
            if (message == null)
                return;

            detail.OffenderUserId = message.SenderUserId;
            detail.OffenderName = getDisplayName(message.SenderUser);
            detail.OffenderEmail = message.SenderUser?.Email;
            detail.ConversationId = message.ConversationId;
            detail.ReportedMessageContent = message.Content;
            return;
        }

        if (report.ReportedEntityType.Equals("JobPosting", StringComparison.OrdinalIgnoreCase))
        {
            var jobPosting = await _reportRepo.GetJobPostingDetailForModeratorAsync(report.ReportedEntityId);
            if (jobPosting == null)
                return;

            detail.JobPostingTitle = jobPosting.Title;
            detail.OffenderUserId = jobPosting.ParentProfile?.UserId;
            detail.OffenderName = getDisplayName(jobPosting.ParentProfile?.User);
            detail.OffenderEmail = jobPosting.ParentProfile?.User?.Email;
            return;
        }

        if (report.ReportedEntityType.Equals("Profile", StringComparison.OrdinalIgnoreCase))
        {
            var user = await _reportRepo.GetUserDetailForModeratorAsync(report.ReportedEntityId);
            detail.OffenderUserId = report.ReportedEntityId;
            detail.OffenderName = getDisplayName(user);
            detail.OffenderEmail = user?.Email;
        }
    }

    private async Task<Guid?> ResolveOffenderUserIdAsync(Report report)
    {
        if (report.ReportedEntityType.Equals("Profile", StringComparison.OrdinalIgnoreCase))
            return report.ReportedEntityId;

        if (report.ReportedEntityType.Equals("Message", StringComparison.OrdinalIgnoreCase))
            return await _reportRepo.GetMessageSenderUserIdAsync(report.ReportedEntityId);

        if (report.ReportedEntityType.Equals("JobPosting", StringComparison.OrdinalIgnoreCase))
            return await _reportRepo.GetJobPostingOwnerUserIdAsync(report.ReportedEntityId);

        return null;
    }

    private static string? validateCreateReportRequest(CreateReportRequest request)
    {
        var ctx = new ValidationContext(request);
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(request, ctx, results, validateAllProperties: true);
        if (ok)
            return null;

        return results.FirstOrDefault()?.ErrorMessage ?? "Dữ liệu báo cáo không hợp lệ.";
    }

    private static string? validateResolveReportRequest(ResolveReportRequest request)
    {
        var ctx = new ValidationContext(request);
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(request, ctx, results, validateAllProperties: true);
        if (ok)
            return null;

        return results.FirstOrDefault()?.ErrorMessage ?? "Dữ liệu xử lý báo cáo không hợp lệ.";
    }
}
