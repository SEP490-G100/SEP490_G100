using Nanny_BackEnd.DTOs.Dashboard;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IModeratorDashboardRepository
{
    Task<int> GetTotalUsersCountAsync();
    Task<int> GetUserCountByRoleAsync(string roleName);
    Task<int> GetUserCountByStatusAsync(int status);
    Task<int> GetPendingVerificationCountAsync();
    Task<int> GetActiveJobPostingCountAsync();
    Task<int> GetTotalContractsCountAsync();
    Task<int> GetPendingJobPostingCountAsync();
    Task<int> GetPendingReportsCountAsync();
    Task<List<DateTime>> GetPendingVerificationDatesSinceAsync(DateTime startDateUtc);
    Task<List<DateTime>> GetPendingJobPostingDatesSinceAsync(DateTime startDateUtc);
    Task<List<DateTime>> GetPendingReportDatesSinceAsync(DateTime startDateUtc);
    Task<List<DashboardModerationEventQueryDto>> GetVerificationModerationEventsSinceAsync(DateTime startDateUtc);
    Task<List<DashboardModerationEventQueryDto>> GetJobPostingModerationEventsSinceAsync(DateTime startDateUtc);
    Task<List<DateTime>> GetUserCreatedDatesSinceAsync(DateTime startDateUtc);
}
