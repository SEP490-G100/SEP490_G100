using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Repositories.Interfaces;

namespace Nanny_BackEnd.Repositories;

public class UserSubscriptionRepository : IUserSubscriptionRepository
{
    private readonly Sep490NannyDbContext _db;

    public UserSubscriptionRepository(Sep490NannyDbContext db) => _db = db;

    /// <summary>Total number of subscriptions (all statuses, not deleted).</summary>
    public async Task<int> GetTotalSubscriptionsAsync() =>
        await _db.UserSubscriptions.CountAsync(s => !s.IsDeleted);

    /// <summary>Total active subscriptions (Status = 1 = Active).</summary>
    public async Task<int> GetActiveSubscriptionsAsync() =>
        await _db.UserSubscriptions.CountAsync(s => !s.IsDeleted && s.Status == 1);

    /// <summary>Total expired/inactive/cancelled subscriptions (Status != Active).</summary>
    public async Task<int> GetExpiredSubscriptionsAsync() =>
        await _db.UserSubscriptions.CountAsync(s => !s.IsDeleted && s.Status != (int)UserSubscriptionStatus.Active);

    /// <summary>Subscription count grouped by month, last N months.</summary>
    public async Task<List<(int Year, int Month, int Count)>> GetMonthlySubscriptionsAsync(int months = 12)
    {
        var since = DateTime.UtcNow.AddMonths(-months);
        var raw = await _db.UserSubscriptions
            .Where(s => !s.IsDeleted && s.CreatedAt >= since)
            .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();

        return raw.Select(r => (r.Year, r.Month, r.Count)).ToList();
    }
}
