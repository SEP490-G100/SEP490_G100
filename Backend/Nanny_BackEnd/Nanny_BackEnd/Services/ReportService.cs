using Nanny_BackEnd.DTOs.Report;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class ReportService
{
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
            ?? throw new KeyNotFoundException("Khong tim thay tin dang hoac tin da bi xoa.");

        if (job.ParentProfile?.UserId == reporterUserId)
            throw new InvalidOperationException("Ban khong the bao cao bai dang cua chinh minh.");

        var report = await createReportAsync(reporterUserId, jobPostingId, "JobPosting", request);

        var reporter = await _userRepo.FindByIdAsync(reporterUserId);
        await _notificationService.createNotificationForModerators(
            "Co bao cao bai dang moi",
            $"{getDisplayName(reporter)} vua gui bao cao cho bai dang \"{job.Title}\".",
            NotificationTypes.ReportSubmitted,
            report.Id,
            "Report",
            reporterUserId);

        return report.Id;
    }

    public async Task<Guid> ReportMessageAsync(Guid messageId, Guid reporterUserId, CreateReportRequest request)
    {
        var message = await _reportRepo.GetMessageForReportAsync(messageId)
            ?? throw new KeyNotFoundException("Khong tim thay tin nhan.");

        if (message.SenderUserId == reporterUserId)
            throw new InvalidOperationException("Ban khong the bao cao tin nhan cua chinh minh.");

        var report = await createReportAsync(reporterUserId, messageId, "Message", request);

        var reporter = await _userRepo.FindByIdAsync(reporterUserId);
        await _notificationService.createNotificationForModerators(
            "Co bao cao moi can xu ly",
            $"{getDisplayName(reporter)} vua gui mot bao cao moi trong he thong.",
            NotificationTypes.ReportSubmitted,
            report.Id,
            "Report",
            reporterUserId);

        return report.Id;
    }

    public async Task<Guid> ReportProfileAsync(Guid profileUserId, Guid reporterUserId, CreateReportRequest request)
    {
        var targetUser = await _reportRepo.GetUserForProfileReportAsync(profileUserId)
            ?? throw new KeyNotFoundException("Khong tim thay ho so can bao cao.");

        if (targetUser.Id == reporterUserId)
            throw new InvalidOperationException("Ban khong the bao cao ho so cua chinh minh.");

        var report = await createReportAsync(reporterUserId, targetUser.Id, "Profile", request);

        var reporter = await _userRepo.FindByIdAsync(reporterUserId);
        await _notificationService.createNotificationForModerators(
            "Co bao cao ho so moi",
            $"{getDisplayName(reporter)} vua gui bao cao ho so nguoi dung.",
            NotificationTypes.ReportSubmitted,
            report.Id,
            "Report",
            reporterUserId);

        return report.Id;
    }

    public async Task<Guid> ReportConversationAsync(Guid conversationId, Guid reporterUserId, CreateReportRequest request)
    {
        var conversation = await _reportRepo.GetConversationForReportAsync(conversationId)
            ?? throw new KeyNotFoundException("Khong tim thay cuoc hoi thoai.");

        var isParticipant = await _reportRepo.IsConversationParticipantAsync(conversation.Id, reporterUserId);
        if (!isParticipant)
            throw new UnauthorizedAccessException("Ban khong phai thanh vien cua cuoc hoi thoai nay.");

        var report = await createReportAsync(reporterUserId, conversation.Id, "Conversation", request);

        var reporter = await _userRepo.FindByIdAsync(reporterUserId);
        await _notificationService.createNotificationForModerators(
            "Co bao cao cuoc hoi thoai moi",
            $"{getDisplayName(reporter)} vua gui bao cao cho mot cuoc hoi thoai.",
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

        return (true, MapDetail(report), null);
    }

    public async Task<(bool Success, int StatusCode, string Message)> ResolveReportAsync(
        Guid id,
        Guid moderatorUserId,
        ResolveReportRequest request)
    {
        var report = await _reportRepo.GetReportByIdAsync(id, includeDeleted: true);
        if (report == null)
            return (false, 404, "Report not found.");

        if (report.IsDeleted)
            return (false, 400, "Cannot resolve a deactivated report.");

        var resolution = request.Resolution?.Trim();
        var actionTaken = request.ActionTaken?.Trim();

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
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Ly do bao cao la bat buoc.");

        var hasPending = await _reportRepo.HasPendingReportAsync(reporterUserId, reportedEntityId, reportedEntityType);
        if (hasPending)
            throw new InvalidOperationException("Ban da gui bao cao nay va dang cho xu ly.");

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = reporterUserId,
            ReportedEntityId = reportedEntityId,
            ReportedEntityType = reportedEntityType,
            Reason = reason,
            Evidence = string.IsNullOrWhiteSpace(request.Evidence) ? null : request.Evidence.Trim(),
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = reporterUserId,
            IsDeleted = false
        };

        _reportRepo.AddReport(report);
        await _reportRepo.SaveChangesAsync();
        return report;
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
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt,
            IsDeleted = report.IsDeleted
        };
    }
}
