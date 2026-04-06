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
}

