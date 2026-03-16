using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class SubscriptionService
{
    private readonly SubscriptionRepository _subscriptionRepo;

    public SubscriptionService(SubscriptionRepository subscriptionRepo)
    {
        _subscriptionRepo = subscriptionRepo;
    }

    public async Task<List<SubscriptionPlanResponse>> getPlans()
    {
        var plans = await _subscriptionRepo.getActivePlans();
        return plans.Select(mapPlan).ToList();
    }

    public async Task<UserSubscriptionResponse?> getCurrentSubscription(Guid userId)
    {
        await expireOldSubscriptions(userId);

        var subscription = await _subscriptionRepo.findCurrentSubscription(userId, DateTime.UtcNow);
        return subscription == null ? null : mapSubscription(subscription);
    }

    public async Task<List<UserSubscriptionResponse>> getHistory(Guid userId)
    {
        await expireOldSubscriptions(userId);

        var subscriptions = await _subscriptionRepo.getSubscriptionHistory(userId);
        return subscriptions.Select(mapSubscription).ToList();
    }

    public async Task<UserSubscriptionResponse> subscribe(Guid userId, SubscribeRequest request)
    {
        await expireOldSubscriptions(userId);

        var plan = await _subscriptionRepo.findPlanById(request.SubscriptionPlanId)
            ?? throw new KeyNotFoundException("Không tìm thấy gói subscription hoặc gói đã ngừng hoạt động.");

        var currentSubscription = await _subscriptionRepo.findCurrentSubscription(userId, DateTime.UtcNow);
        if (currentSubscription != null)
            throw new InvalidOperationException("Bạn đang có gói subscription còn hiệu lực. Vui lòng hủy hoặc chờ gói hiện tại hết hạn.");

        var nowUtc = DateTime.UtcNow;
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = plan.Price,
            PaymentGatewayTransactionId = request.PaymentGatewayTransactionId?.Trim(),
            Status = 2,
            Description = $"Thanh toán gói {plan.Name}",
            Type = 2,
            CreatedAt = nowUtc,
            CompletedAt = nowUtc,
            CreatedBy = userId
        };

        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionPlanId = plan.Id,
            StartDate = nowUtc,
            EndDate = nowUtc.AddDays(plan.DurationDays),
            Status = 1,
            PaymentTransactionId = transaction.Id,
            CreatedAt = nowUtc,
            CreatedBy = userId
        };

        _subscriptionRepo.addTransaction(transaction);
        _subscriptionRepo.addUserSubscription(subscription);
        await _subscriptionRepo.saveChanges();

        subscription.SubscriptionPlan = plan;
        subscription.PaymentTransaction = transaction;
        return mapSubscription(subscription);
    }

    public async Task<UserSubscriptionResponse> cancelCurrentSubscription(Guid userId)
    {
        await expireOldSubscriptions(userId);

        var subscription = await _subscriptionRepo.findCurrentSubscription(userId, DateTime.UtcNow)
            ?? throw new KeyNotFoundException("Bạn không có gói subscription đang hoạt động.");

        var nowUtc = DateTime.UtcNow;
        subscription.Status = 2;
        subscription.CancelledAt = nowUtc;
        subscription.EndDate = nowUtc;
        subscription.UpdatedAt = nowUtc;
        subscription.UpdatedBy = userId;

        await _subscriptionRepo.saveChanges();
        return mapSubscription(subscription);
    }

    private async Task expireOldSubscriptions(Guid userId)
    {
        var nowUtc = DateTime.UtcNow;
        var expiredSubscriptions = await _subscriptionRepo.getExpiredActiveSubscriptions(userId, nowUtc);
        if (expiredSubscriptions.Count == 0)
            return;

        foreach (var subscription in expiredSubscriptions)
        {
            subscription.Status = 3;
            subscription.UpdatedAt = nowUtc;
            subscription.CancelledAt ??= subscription.EndDate;
        }

        await _subscriptionRepo.saveChanges();
    }

    private static SubscriptionPlanResponse mapPlan(SubscriptionPlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        Description = plan.Description,
        Price = plan.Price,
        DurationDays = plan.DurationDays,
        Features = splitFeatures(plan.Features),
        SortOrder = plan.SortOrder
    };

    private static UserSubscriptionResponse mapSubscription(UserSubscription subscription)
    {
        var nowUtc = DateTime.UtcNow;
        return new UserSubscriptionResponse
        {
            Id = subscription.Id,
            Status = subscription.Status,
            StatusLabel = getSubscriptionStatusLabel(subscription.Status),
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            CancelledAt = subscription.CancelledAt,
            IsActive = subscription.Status == 1 && subscription.EndDate >= nowUtc,
            RemainingDays = Math.Max(0, (int)Math.Ceiling((subscription.EndDate - nowUtc).TotalDays)),
            Plan = mapPlan(subscription.SubscriptionPlan),
            Transaction = subscription.PaymentTransaction == null ? null : mapTransaction(subscription.PaymentTransaction)
        };
    }

    private static SubscriptionTransactionResponse mapTransaction(Transaction transaction) => new()
    {
        Id = transaction.Id,
        Amount = transaction.Amount,
        Status = transaction.Status,
        StatusLabel = getTransactionStatusLabel(transaction.Status),
        Type = transaction.Type,
        TypeLabel = getTransactionTypeLabel(transaction.Type),
        Description = transaction.Description,
        PaymentGatewayTransactionId = transaction.PaymentGatewayTransactionId,
        CreatedAt = transaction.CreatedAt,
        CompletedAt = transaction.CompletedAt
    };

    private static List<string> splitFeatures(string? features) =>
        string.IsNullOrWhiteSpace(features)
            ? []
            : features
                .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    private static string getSubscriptionStatusLabel(int status) => status switch
    {
        1 => "Đang hoạt động",
        2 => "Đã hủy",
        3 => "Hết hạn",
        _ => "Không xác định"
    };

    private static string getTransactionStatusLabel(int status) => status switch
    {
        1 => "Chờ thanh toán",
        2 => "Thành công",
        3 => "Thất bại",
        4 => "Đã hoàn tiền",
        _ => "Không xác định"
    };

    private static string getTransactionTypeLabel(int type) => type switch
    {
        1 => "Nạp tiền",
        2 => "Thanh toán subscription",
        3 => "Hoàn tiền",
        _ => "Khác"
    };
}
