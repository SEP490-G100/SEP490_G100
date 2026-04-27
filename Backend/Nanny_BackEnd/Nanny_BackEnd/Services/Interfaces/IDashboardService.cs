using Nanny_BackEnd.DTOs.Dashboard;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IDashboardService
{
    Task<AdminDashboardStatsDto> GetAdminDashboardStatsAsync();
    Task<ModeratorDashboardStatsDto> GetModeratorDashboardStatsAsync();
}
