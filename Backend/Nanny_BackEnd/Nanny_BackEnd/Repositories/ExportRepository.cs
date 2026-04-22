using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ExportRepository : IExportRepository
{
    private readonly Sep490NannyDbContext _db;

    public ExportRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<(decimal TotalRevenue, decimal CurrentMonthRevenue, int TotalSubscriptions, int CurrentMonthSubscriptions)>
        GetExportSummaryAsync(DateTime nowUtc)
    {
        var startOfMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1);

        var totalRevenue = await _db.Transactions
            .Where(t => !t.IsDeleted && t.Status == 1)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var currentMonthRevenue = await _db.Transactions
            .Where(t => !t.IsDeleted && t.Status == 1 && t.CreatedAt >= startOfMonth)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var totalSubscriptions = await _db.UserSubscriptions.CountAsync(s => !s.IsDeleted);

        var currentMonthSubscriptions = await _db.UserSubscriptions
            .CountAsync(s => !s.IsDeleted && s.CreatedAt >= startOfMonth);

        return (totalRevenue, currentMonthRevenue, totalSubscriptions, currentMonthSubscriptions);
    }

    public async Task<List<User>> GetUsersForExportAsync() =>
        await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

    public async Task<List<Transaction>> GetTransactionsForExportAsync() =>
        await _db.Transactions
            .Include(t => t.User)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<List<UserSubscription>> GetSubscriptionsForExportAsync() =>
        await _db.UserSubscriptions
            .Include(s => s.User)
            .Include(s => s.SubscriptionPlan)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
}
