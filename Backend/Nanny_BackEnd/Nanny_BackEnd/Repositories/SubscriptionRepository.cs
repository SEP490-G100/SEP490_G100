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

    public async Task<List<SubscriptionPlan>> getPlansByNames(IEnumerable<string> names)
    {
        var normalizedNames = names.ToList();
        return await _db.SubscriptionPlans
            .Where(p => !p.IsDeleted && normalizedNames.Contains(p.Name))
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<SubscriptionPlan?> findPlanById(Guid planId) =>
        await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted && p.IsActive);

    public async Task<UserSubscription?> findCurrentSubscription(Guid userId, DateTime nowUtc) =>
        await _db.UserSubscriptions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .Include(s => s.SubscriptionPlan)
            .Include(s => s.PaymentTransaction)
            .Where(s => s.Status == 1 && s.EndDate >= nowUtc)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefaultAsync();

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

    public void addTransaction(Transaction transaction) => _db.Transactions.Add(transaction);

    public void addUserSubscription(UserSubscription subscription) => _db.UserSubscriptions.Add(subscription);

    public void addPlan(SubscriptionPlan plan) => _db.SubscriptionPlans.Add(plan);

    public async Task saveChanges() => await _db.SaveChangesAsync();
}
