using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IAdminNotificationRepository
{
    Task<List<Notification>> GetAdminNotificationRowsAsync(string? search, bool? isDeleted);
    Task<List<Notification>> GetAdminNotificationRowsByBroadcastIdAsync(Guid broadcastId);
    Task<List<string>> GetNotificationAssignableRolesAsync();
    Task<List<Guid>> GetActiveUserIdsByRoleAsync(string roleName);
    Task<List<Guid>> GetActiveUserIdsByRolesAsync(IEnumerable<string> roleNames);
    void AddNotifications(IEnumerable<Notification> notifications);
    Task SaveChangesAsync();
}
