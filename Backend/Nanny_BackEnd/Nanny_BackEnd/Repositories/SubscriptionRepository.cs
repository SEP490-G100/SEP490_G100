using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class SubscriptionRepository
{
    private readonly Sep490NannyDbContext _db;

    public SubscriptionRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<List<SubscriptionPlan>> getActivePlans() =>
        await _db.SubscriptionPlans
            .Where(p => !p.IsDeleted && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ToListAsync();

    /// <summary>Admin: all plans including inactive (but not soft-deleted).</summary>
    public async Task<List<SubscriptionPlan>> getAllPlansIncludingInactive() =>
        await _db.SubscriptionPlans
            .Include(p => p.UserSubscriptions)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ToListAsync();

    /// <summary>Admin: find plan by ID including inactive, with subscriber count.</summary>
    public async Task<SubscriptionPlan?> findPlanByIdIncludingInactive(Guid planId) =>
        await _db.SubscriptionPlans
            .Include(p => p.UserSubscriptions)
            .FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted);

    public async Task<List<SubscriptionPlan>> getPlansByNames(IEnumerable<string> names)
    {
        var normalizedNames = names.ToList();
        return await _db.SubscriptionPlans
            .Where(p => !p.IsDeleted && normalizedNames.Contains(p.Name))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<List<SubscriptionPlan>> getPlansByNamesIncludingDeleted(IEnumerable<string> names)
    {
        var normalizedNames = names.ToList();
        return await _db.SubscriptionPlans
            .Where(p => normalizedNames.Contains(p.Name))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<SubscriptionPlan?> findPlanById(Guid planId) =>
        await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted && p.IsActive);

    public async Task<SubscriptionPlan?> findAdminPlanById(Guid planId) =>
        await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted);

    public async Task<SubscriptionPlan?> findAdminPlanByIdIncludingDeleted(Guid planId) =>
        await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId);

    public async Task<List<SubscriptionPlan>> getAdminPlans() =>
        await _db.SubscriptionPlans
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ThenBy(p => p.Name)
            .ToListAsync();

    public async Task<List<SubscriptionPlan>> getAdminPlansIncludingDeleted() =>
        await _db.SubscriptionPlans
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ThenBy(p => p.Name)
            .ToListAsync();

    public async Task<bool> existsPlanName(string name, Guid? excludeId = null) =>
        await _db.SubscriptionPlans
            .AnyAsync(p => !p.IsDeleted &&
                           p.Name == name &&
                           (!excludeId.HasValue || p.Id != excludeId.Value));

    public async Task<bool> existsPlanNameIncludingDeleted(string name, Guid? excludeId = null) =>
        await _db.SubscriptionPlans
            .AnyAsync(p => p.Name == name &&
                           (!excludeId.HasValue || p.Id != excludeId.Value));

    public async Task<int> getNextSubscriptionPlanSortOrder() =>
        (await _db.SubscriptionPlans
            .Select(p => (int?)p.SortOrder)
            .MaxAsync() ?? 0) + 1;

    public async Task<int> countActiveSubscriptionsByPlan(Guid planId, DateTime nowUtc) =>
        await _db.UserSubscriptions
            .CountAsync(s => s.SubscriptionPlanId == planId &&
                             !s.IsDeleted &&
                             s.Status == 1 &&
                             s.EndDate >= nowUtc);

    public async Task<UserSubscription?> findCurrentSubscription(Guid userId, DateTime nowUtc) =>
        await _db.UserSubscriptions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .Include(s => s.SubscriptionPlan)
            .Include(s => s.PaymentTransaction)
            .Where(s => s.Status == 1 && s.EndDate >= nowUtc)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

    public async Task<bool> hasAnySubscriptionLinkedToTransaction(Guid transactionId) =>
        await _db.UserSubscriptions.AnyAsync(s => s.PaymentTransactionId == transactionId && !s.IsDeleted);

    public async Task<UserSubscription?> findCurrentSubscriptionByParentProfile(Guid parentProfileId, DateTime nowUtc) =>
        await _db.UserSubscriptions
            .Where(s => !s.IsDeleted && s.Status == 1 && s.EndDate >= nowUtc)
            .Include(s => s.SubscriptionPlan)
            .Include(s => s.PaymentTransaction)
            .Where(s => _db.ParentProfiles.Any(p => p.Id == parentProfileId && !p.IsDeleted && p.UserId == s.UserId))
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

    public async Task<UserSubscription?> findCurrentSubscriptionByNannyProfile(Guid nannyProfileId, DateTime nowUtc) =>
        await _db.UserSubscriptions
            .Where(s => !s.IsDeleted && s.Status == 1 && s.EndDate >= nowUtc)
            .Include(s => s.SubscriptionPlan)
            .Include(s => s.PaymentTransaction)
            .Where(s => _db.NannyProfiles.Any(n => n.Id == nannyProfileId && !n.IsDeleted && n.UserId == s.UserId))
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

    public async Task<bool> hasParentProfile(Guid userId) =>
        await _db.ParentProfiles.AnyAsync(p => p.UserId == userId && !p.IsDeleted);

    public async Task<bool> hasNannyProfile(Guid userId) =>
        await _db.NannyProfiles.AnyAsync(n => n.UserId == userId && !n.IsDeleted);

    public async Task<List<UserSubscription>> getSubscriptionHistory(Guid userId) =>
        await _db.UserSubscriptions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .Include(s => s.SubscriptionPlan)
            .Include(s => s.PaymentTransaction)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<List<UserSubscription>> getExpiredActiveSubscriptions(Guid userId, DateTime nowUtc) =>
        await _db.UserSubscriptions
            .Where(s => s.UserId == userId && !s.IsDeleted && s.Status == 1 && s.EndDate < nowUtc)
            .ToListAsync();

    public async Task<List<UserSubscription>> getActiveSubscriptionsExpiringOnDate(DateTime targetDateUtc) =>
        await _db.UserSubscriptions
            .Where(s => !s.IsDeleted && s.Status == 1)
            .Include(s => s.SubscriptionPlan)
            .Where(s => s.EndDate.Date == targetDateUtc.Date)
            .ToListAsync();

    public async Task<bool> hasNotificationForSubscription(Guid userId, Guid subscriptionId, string title) =>
        await _db.Notifications.AnyAsync(n =>
            n.UserId == userId &&
            !n.IsDeleted &&
            n.RelatedEntityId == subscriptionId &&
            n.RelatedEntityType == "UserSubscription" &&
            n.Title == title);

    public void addNotification(Notification notification) => _db.Notifications.Add(notification);

    public async Task<List<Notification>> getNotifications(Guid userId, int page, int pageSize) =>
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<int> countNotifications(Guid userId) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsDeleted);

    public async Task<int> countUnreadNotifications(Guid userId) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsDeleted && !n.IsRead);

    public async Task<Notification?> findNotificationById(Guid notificationId, Guid userId) =>
        await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId && !n.IsDeleted);

    public async Task<List<Notification>> getUnreadNotifications(Guid userId) =>
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<List<Notification>> getAdminNotificationRows(string? search = null, bool? isDeleted = null)
    {
        var query = _db.Notifications
            .Where(n => n.Type == Helpers.NotificationTypes.AdminBroadcast
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

    public async Task<List<Notification>> getAdminNotificationRowsByBroadcastId(Guid broadcastId) =>
        await _db.Notifications
            .Where(n => n.Type == Helpers.NotificationTypes.AdminBroadcast
                && n.RelatedEntityId == broadcastId
                && n.RelatedEntityType != null
                && n.RelatedEntityType.StartsWith("AdminNotification"))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<List<User>> getUsersByIds(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        return await _db.Users
            .Where(u => ids.Contains(u.Id) && !u.IsDeleted)
            .ToListAsync();
    }

    public async Task<Transaction?> findTransactionById(Guid transactionId, Guid userId) =>
        await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId && !t.IsDeleted);

    public async Task<List<Transaction>> getPendingSubscriptionTransactions(Guid userId) =>
        await _db.Transactions
            .Where(t => t.UserId == userId && !t.IsDeleted && t.Type == 1 && (t.Status == 1 || t.Status == 5))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<Transaction?> findTransactionByGatewayCode(string gatewayCode) =>
        await _db.Transactions
            .FirstOrDefaultAsync(t => t.PaymentGatewayTransactionId == gatewayCode && !t.IsDeleted);

    public async Task<bool> existsGatewayTransactionCode(string gatewayCode) =>
        await _db.Transactions
            .AnyAsync(t => t.PaymentGatewayTransactionId == gatewayCode && !t.IsDeleted);

    public async Task<List<Transaction>> getUserSubscriptionTransactions(Guid userId, int maxItems = 30) =>
        await _db.Transactions
            .Where(t => t.UserId == userId && !t.IsDeleted && t.Type == 1)
            .OrderByDescending(t => t.CreatedAt)
            .Take(maxItems)
            .ToListAsync();

    public void addTransaction(Transaction transaction) => _db.Transactions.Add(transaction);

    public void addUserSubscription(UserSubscription subscription) => _db.UserSubscriptions.Add(subscription);

    public void addPlan(SubscriptionPlan plan) => _db.SubscriptionPlans.Add(plan);

    public async Task saveChanges() => await _db.SaveChangesAsync();
}
