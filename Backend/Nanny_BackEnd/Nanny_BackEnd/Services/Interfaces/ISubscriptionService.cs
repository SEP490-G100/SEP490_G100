using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Subscription;

namespace Nanny_BackEnd.Services.Interfaces;

public interface ISubscriptionService
{
    Task<List<SubscriptionPlanResponse>> getPlans();
    Task<SubscriptionPlanResponse?> getPlanByCode(string code);
    Task<UserSubscriptionResponse?> getCurrentSubscription(Guid userId);
    Task<List<UserSubscriptionResponse>> getHistory(Guid userId);
    Task<List<SubscriptionTransactionResponse>> getTransactionHistory(Guid userId);
    Task<UserSubscriptionResponse> subscribe(Guid userId, SubscribeRequest request);
    Task<SubscriptionPaymentSessionResponse> createPayment(Guid userId, CreateSubscriptionPaymentRequest request);
    Task<SubscriptionPaymentStatusResponse> getPaymentStatus(Guid userId, Guid transactionId);
    Task<MarkSubscriptionTransferredResponse> markTransferred(Guid userId, Guid transactionId);
    Task<int> handleCassoWebhook(CassoWebhookRequest request);
    Task<int> handlePayOsWebhook(PayOsWebhookRequest request);
    Task<UserSubscriptionResponse> cancelCurrentSubscription(Guid userId);

    Task<SubscriptionBenefitResponse> getBenefitsForParentProfile(Guid parentProfileId);
    Task<SubscriptionBenefitResponse> getBenefitsForNannyProfile(Guid nannyProfileId);
    Task<bool> hasActiveParentSubscription(Guid parentProfileId);

    Task<AdminSubscriptionPlanListResponse> getAdminPlans(
        string? search,
        string? targetRole,
        bool? isActive,
        int page,
        int pageSize);

    Task<AdminSubscriptionPlanDetailResponse?> getAdminPlanDetail(Guid id);
    Task<AdminSubscriptionPlanDetailResponse> createAdminPlan(Guid adminUserId, AdminSubscriptionPlanUpsertRequest request);
    Task<AdminSubscriptionPlanDetailResponse> updateAdminPlan(Guid id, Guid adminUserId, AdminSubscriptionPlanUpsertRequest request);
    Task toggleAdminPlanStatus(Guid id, Guid adminUserId, bool isActive);
}
