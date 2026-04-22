using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IReportRepository
{
    Task<JobPosting?> GetJobPostingForReportAsync(Guid jobPostingId);
    Task<Message?> GetMessageForReportAsync(Guid messageId);
    Task<User?> GetUserForProfileReportAsync(Guid userId);
    Task<Guid?> GetJobPostingOwnerUserIdAsync(Guid jobPostingId);
    Task<Guid?> GetMessageSenderUserIdAsync(Guid messageId);
    Task<Message?> GetMessageDetailForModeratorAsync(Guid messageId);
    Task<JobPosting?> GetJobPostingDetailForModeratorAsync(Guid jobPostingId);
    Task<User?> GetUserDetailForModeratorAsync(Guid userId);
    Task<bool> HasPendingReportAsync(Guid reporterUserId, Guid reportedEntityId, string reportedEntityType);
    Task<int> CountReportsSinceAsync(Guid reporterUserId, DateTime sinceUtc);
    Task<DateTime?> GetOldestReportCreatedAtSinceAsync(Guid reporterUserId, DateTime sinceUtc);
    Task<DateTime?> GetLatestCompletedReportMomentAsync(
        Guid reporterUserId,
        Guid reportedEntityId,
        string reportedEntityType);
    Task<(List<Report> Items, int TotalCount)> GetPagedReportsAsync(
        int? status,
        string? entityType,
        string? search,
        int page,
        int pageSize);
    Task<Report?> GetReportByIdAsync(Guid id, bool includeDeleted = false);
    void AddReport(Report report);
    Task SaveChangesAsync();
}
