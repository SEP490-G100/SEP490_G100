using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class AdminSubcriptionPlanRepository
{
    private readonly SubscriptionRepository _subscriptionRepository;

    public AdminSubcriptionPlanRepository(SubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public async Task<List<SubscriptionPlan>> GetAdminPlansIncludingDeletedAsync() =>
        await _subscriptionRepository.getAdminPlansIncludingDeleted();

    public async Task<SubscriptionPlan?> FindAdminPlanByIdIncludingDeletedAsync(Guid planId) =>
        await _subscriptionRepository.findAdminPlanByIdIncludingDeleted(planId);

    public async Task<bool> ExistsPlanNameIncludingDeletedAsync(string name, Guid? excludeId = null) =>
        await _subscriptionRepository.existsPlanNameIncludingDeleted(name, excludeId);

    public async Task<int> GetNextSubscriptionPlanSortOrderAsync() =>
        await _subscriptionRepository.getNextSubscriptionPlanSortOrder();

    public async Task<int> CountActiveSubscriptionsByPlanAsync(Guid planId, DateTime nowUtc) =>
        await _subscriptionRepository.countActiveSubscriptionsByPlan(planId, nowUtc);

    public void AddPlan(SubscriptionPlan plan) =>
        _subscriptionRepository.addPlan(plan);

    public async Task SaveChangesAsync() =>
        await _subscriptionRepository.saveChanges();
}
