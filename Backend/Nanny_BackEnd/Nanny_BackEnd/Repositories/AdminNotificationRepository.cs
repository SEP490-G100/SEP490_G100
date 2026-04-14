using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class AdminNotificationRepository
{
    private readonly Sep490NannyDbContext _db;

    public AdminNotificationRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<List<Notification>> GetAdminNotificationRowsAsync(string? search, bool? isDeleted)
    {
        var query = _db.Notifications
            .Where(n => n.Type == NotificationTypes.AdminBroadcast
                        && n.RelatedEntityId.HasValue
                        && n.RelatedEntityType != null
                        && n.RelatedEntityType.StartsWith("AdminNotification"))
            .AsQueryable();

        if (isDeleted.HasValue)
            query = query.Where(n => n.IsDeleted == isDeleted.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(n =>
                n.Title.ToLower().Contains(keyword) ||
                n.Content.ToLower().Contains(keyword));
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetAdminNotificationRowsByBroadcastIdAsync(Guid broadcastId) =>
        await _db.Notifications
            .Where(n => n.Type == NotificationTypes.AdminBroadcast
                        && n.RelatedEntityId == broadcastId
                        && n.RelatedEntityType != null
                        && n.RelatedEntityType.StartsWith("AdminNotification"))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<List<string>> GetNotificationAssignableRolesAsync() =>
        await _db.Roles
            .Where(r => !r.IsDeleted && r.Name != "Admin")
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync();

    public async Task<List<Guid>> GetActiveUserIdsByRoleAsync(string roleName) =>
        await _db.Users
            .Where(u => !u.IsDeleted
                        && u.Status == 1
                        && u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == roleName))
            .Select(u => u.Id)
            .ToListAsync();

    public async Task<List<Guid>> GetActiveUserIdsByRolesAsync(IEnumerable<string> roleNames)
    {
        var normalizedRoles = roleNames
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedRoles.Count == 0)
            return [];

        return await _db.Users
            .Where(u => !u.IsDeleted
                        && u.Status == 1
                        && u.UserRoles.Any(ur => !ur.IsDeleted && normalizedRoles.Contains(ur.Role.Name)))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync();
    }

    public void AddNotifications(IEnumerable<Notification> notifications) =>
        _db.Notifications.AddRange(notifications);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
