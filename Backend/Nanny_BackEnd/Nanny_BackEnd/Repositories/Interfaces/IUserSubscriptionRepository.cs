namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IUserSubscriptionRepository
{
    Task<int> GetTotalSubscriptionsAsync();
    Task<int> GetActiveSubscriptionsAsync();
    Task<int> GetExpiredSubscriptionsAsync();
    Task<List<(int Year, int Month, int Count)>> GetMonthlySubscriptionsAsync(int months = 12);
}
