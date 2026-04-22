using System;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Subscription;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IAdminSubcriptionPlanService
{
    Task<AdminSubscriptionPlanListResponse> AdminViewSubscriptionPlanListAsync(
        string? search,
        string? targetRole,
        bool? isActive,
        int page,
        int pageSize);

    Task<AdminSubscriptionPlanDetailResponse?> AdminViewSubscriptionPlanDetailAsync(Guid id);

    Task<AdminSubscriptionPlanDetailResponse> AdminCreateSubscriptionPlanAsync(
        Guid adminUserId,
        AdminSubscriptionPlanUpsertRequest request);

    Task<AdminSubscriptionPlanDetailResponse> AdminUpdateSubscriptionPlanAsync(
        Guid id,
        Guid adminUserId,
        AdminSubscriptionPlanUpsertRequest request);

    Task AdminUpdateSubscriptionPlanStatusAsync(Guid id, Guid adminUserId, bool isActive);
}
