using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class TransactionRepository
{
    private readonly Sep490NannyDbContext _db;

    public TransactionRepository(Sep490NannyDbContext db) => _db = db;

    /// <summary>Total revenue from completed transactions (Status = Completed = 2).</summary>
    public async Task<decimal> GetTotalRevenueAsync() =>
        await _db.Transactions
            .Where(t => !t.IsDeleted && t.Status == (int)TransactionStatus.Completed)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

    /// <summary>Total revenue grouped by month (for charts), last N months.</summary>
    public async Task<List<(int Year, int Month, decimal Revenue)>> GetMonthlyRevenueAsync(int months = 12)
    {
        var since = DateTime.UtcNow.AddMonths(-months);
        var raw = await _db.Transactions
            .Where(t => !t.IsDeleted && t.Status == (int)TransactionStatus.Completed && t.CreatedAt >= since)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Revenue = g.Sum(t => t.Amount) })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();

        return raw.Select(r => (r.Year, r.Month, r.Revenue)).ToList();
    }

    /// <summary>5 most recent transactions with user info.</summary>
    public async Task<List<Transaction>> GetRecentTransactionsAsync(int count = 5) =>
        await _db.Transactions
            .Include(t => t.User)
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync();
}
