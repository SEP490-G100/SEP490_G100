using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface ISubscriptionRepository
{
    Task<List<SubscriptionPlan>> getActivePlans();
    Task<List<SubscriptionPlan>> getAllPlansIncludingInactive();
    Task<SubscriptionPlan?> findPlanByIdIncludingInactive(Guid planId);
    Task<List<SubscriptionPlan>> getPlansByNames(IEnumerable<string> names);
    Task<List<SubscriptionPlan>> getPlansByNamesIncludingDeleted(IEnumerable<string> names);
    Task<SubscriptionPlan?> findPlanById(Guid planId);
    Task<SubscriptionPlan?> findAdminPlanById(Guid planId);
    Task<SubscriptionPlan?> findAdminPlanByIdIncludingDeleted(Guid planId);
    Task<List<SubscriptionPlan>> getAdminPlans();
    Task<List<SubscriptionPlan>> getAdminPlansIncludingDeleted();
    Task<bool> existsPlanName(string name, Guid? excludeId = null);
    Task<bool> existsPlanNameIncludingDeleted(string name, Guid? excludeId = null);
    Task<int> getNextSubscriptionPlanSortOrder();
    Task<int> countActiveSubscriptionsByPlan(Guid planId, DateTime nowUtc);
    Task<UserSubscription?> findCurrentSubscription(Guid userId, DateTime nowUtc);
    Task<bool> hasAnySubscriptionLinkedToTransaction(Guid transactionId);
    Task<UserSubscription?> findCurrentSubscriptionByParentProfile(Guid parentProfileId, DateTime nowUtc);
    Task<UserSubscription?> findCurrentSubscriptionByNannyProfile(Guid nannyProfileId, DateTime nowUtc);
    Task<bool> hasParentProfile(Guid userId);
    Task<bool> hasNannyProfile(Guid userId);
    Task<List<UserSubscription>> getSubscriptionHistory(Guid userId);
    Task<List<UserSubscription>> getExpiredActiveSubscriptions(Guid userId, DateTime nowUtc);
    Task<List<UserSubscription>> getActiveSubscriptionsExpiringOnDate(DateTime targetDateUtc);
    Task<bool> hasNotificationForSubscription(Guid userId, Guid subscriptionId, string title);
    void addNotification(Notification notification);
    Task<List<Notification>> getNotifications(Guid userId, int page, int pageSize);
    Task<int> countNotifications(Guid userId);
    Task<int> countUnreadNotifications(Guid userId);
    Task<Notification?> findNotificationById(Guid notificationId, Guid userId);
    Task<List<Notification>> getUnreadNotifications(Guid userId);
    Task<List<Notification>> getAdminNotificationRows(string? search = null, bool? isDeleted = null);
    Task<List<Notification>> getAdminNotificationRowsByBroadcastId(Guid broadcastId);
    Task<List<User>> getUsersByIds(IEnumerable<Guid> userIds);
    Task<Transaction?> findTransactionById(Guid transactionId, Guid userId);
    Task<List<Transaction>> getPendingSubscriptionTransactions(Guid userId);
    Task<Transaction?> findTransactionByGatewayCode(string gatewayCode);
    Task<bool> existsGatewayTransactionCode(string gatewayCode);
    Task<List<Transaction>> getUserSubscriptionTransactions(Guid userId, int maxItems = 30);
    void addTransaction(Transaction transaction);
    void addUserSubscription(UserSubscription subscription);
    void addPlan(SubscriptionPlan plan);
    Task saveChanges();
}
