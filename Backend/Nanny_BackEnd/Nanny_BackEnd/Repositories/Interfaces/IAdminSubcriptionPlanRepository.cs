using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IAdminSubcriptionPlanRepository
{
    Task<List<SubscriptionPlan>> GetAdminPlansIncludingDeletedAsync();
    Task<SubscriptionPlan?> FindAdminPlanByIdIncludingDeletedAsync(Guid planId);
    Task<bool> ExistsPlanNameIncludingDeletedAsync(string name, Guid? excludeId = null);
    Task<int> GetNextSubscriptionPlanSortOrderAsync();
    Task<int> CountActiveSubscriptionsByPlanAsync(Guid planId, DateTime nowUtc);
    void AddPlan(SubscriptionPlan plan);
    Task SaveChangesAsync();
}
