using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface ITransactionRepository
{
    Task<decimal> GetTotalRevenueAsync();
    Task<List<(int Year, int Month, decimal Revenue)>> GetMonthlyRevenueAsync(int months = 12);
    Task<List<Transaction>> GetRecentTransactionsAsync(int count = 5);
}
