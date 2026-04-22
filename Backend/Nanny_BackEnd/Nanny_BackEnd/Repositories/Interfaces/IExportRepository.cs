using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IExportRepository
{
    Task<(decimal TotalRevenue, decimal CurrentMonthRevenue, int TotalSubscriptions, int CurrentMonthSubscriptions)>
        GetExportSummaryAsync(DateTime nowUtc);
    Task<List<User>> GetUsersForExportAsync();
    Task<List<Transaction>> GetTransactionsForExportAsync();
    Task<List<UserSubscription>> GetSubscriptionsForExportAsync();
}
