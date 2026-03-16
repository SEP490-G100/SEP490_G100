using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using System.Text.Json;

namespace Nanny_BackEnd.Services;

public class SubscriptionService
{
    private sealed record ManagedPlanDefinition(
        string Code,
        string Name,
        string Description,
        decimal Price,
        int DurationDays,
        int SortOrder,
        SubscriptionBenefitResponse Benefits,
        List<string> Features);

    private static readonly ManagedPlanDefinition PlusPlan = new(
        Code: "PLUS",
        Name: "Plus",
        Description: "Goi Plus danh cho phu huynh dang bai thuong xuyen, can bai dang noi bat hon va quan ly hieu qua hon.",
        Price: 299000m,
        DurationDays: 30,
        SortOrder: 1,
        Benefits: new SubscriptionBenefitResponse
        {
            MonthlyJobPostLimit = 10,
            FeaturedBadge = true,
            SearchPriority = false,
            ListingDurationDays = 45
        },
        Features:
        [
            "Dang toi da 10 bai moi moi thang",
            "Bai dang co badge noi bat",
            "Thoi gian hien thi bai dang 45 ngay"
        ]);

    private static readonly ManagedPlanDefinition ProPlan = new(
        Code: "PRO",
        Name: "Pro",
        Description: "Goi Pro danh cho phu huynh dang tuyen nghiem tuc, can uu tien hien thi va hieu qua tiep can cao hon.",
        Price: 499000m,
        DurationDays: 30,
        SortOrder: 2,
        Benefits: new SubscriptionBenefitResponse
        {
            MonthlyJobPostLimit = 30,
            FeaturedBadge = true,
            SearchPriority = true,
            ListingDurationDays = 60
        },
        Features:
        [
            "Dang toi da 30 bai moi moi thang",
            "Bai dang co badge noi bat",
            "Duoc uu tien hien thi trong ket qua tim kiem",
            "Thoi gian hien thi bai dang 60 ngay"
        ]);

    private static readonly ManagedPlanDefinition[] ManagedPlans = [PlusPlan, ProPlan];

    private readonly SubscriptionRepository _subscriptionRepo;

    public SubscriptionService(SubscriptionRepository subscriptionRepo)
    {
        _subscriptionRepo = subscriptionRepo;
    }

    public async Task<List<SubscriptionPlanResponse>> getPlans()
    {
        var plans = await ensureManagedPlans();
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

        await ensureManagedPlans();
        var plan = await _subscriptionRepo.findPlanById(request.SubscriptionPlanId)
            ?? throw new KeyNotFoundException("Không tìm thấy gói subscription hoặc gói đã ngừng hoạt động.");

        if (getManagedPlanDefinition(plan.Name) == null)
            throw new InvalidOperationException("Hệ thống hiện chỉ hỗ trợ 2 gói subscription là Plus và Pro.");

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
            Type = 1,
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

    public async Task<SubscriptionPlanResponse?> getPlanByCode(string code)
    {
        var plans = await ensureManagedPlans();
        var plan = plans.FirstOrDefault(p =>
            string.Equals(getManagedPlanDefinition(p.Name)?.Code, code, StringComparison.OrdinalIgnoreCase));
        return plan == null ? null : mapPlan(plan);
    }

    public async Task<SubscriptionBenefitResponse> getBenefitsForParentProfile(Guid parentProfileId)
    {
        await ensureManagedPlans();
        var subscription = await _subscriptionRepo.findCurrentSubscriptionByParentProfile(parentProfileId, DateTime.UtcNow);
        var definition = getManagedPlanDefinition(subscription?.SubscriptionPlan?.Name);
        return definition?.Benefits ?? SubscriptionBenefitResponse.Free;
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

    private async Task<List<SubscriptionPlan>> ensureManagedPlans()
    {
        var existingPlans = await _subscriptionRepo.getPlansByNames(ManagedPlans.Select(p => p.Name));
        var planMap = existingPlans.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var needsSave = false;

        foreach (var definition in ManagedPlans)
        {
            if (!planMap.TryGetValue(definition.Name, out var plan))
            {
                plan = new SubscriptionPlan
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                applyDefinition(plan, definition);
                _subscriptionRepo.addPlan(plan);
                existingPlans.Add(plan);
                needsSave = true;
                continue;
            }

            if (applyDefinition(plan, definition))
                needsSave = true;
        }

        if (needsSave)
            await _subscriptionRepo.saveChanges();

        return existingPlans
            .Where(p => !p.IsDeleted && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Price)
            .ToList();
    }

    private static bool applyDefinition(SubscriptionPlan plan, ManagedPlanDefinition definition)
    {
        var changed = false;

        if (plan.Name != definition.Name)
        {
            plan.Name = definition.Name;
            changed = true;
        }

        if (plan.Description != definition.Description)
        {
            plan.Description = definition.Description;
            changed = true;
        }

        if (plan.Price != definition.Price)
        {
            plan.Price = definition.Price;
            changed = true;
        }

        if (plan.DurationDays != definition.DurationDays)
        {
            plan.DurationDays = definition.DurationDays;
            changed = true;
        }

        if (plan.SortOrder != definition.SortOrder)
        {
            plan.SortOrder = definition.SortOrder;
            changed = true;
        }

        var features = JsonSerializer.Serialize(definition.Features);
        if (plan.Features != features)
        {
            plan.Features = features;
            changed = true;
        }

        if (!plan.IsActive)
        {
            plan.IsActive = true;
            changed = true;
        }

        if (plan.IsDeleted)
        {
            plan.IsDeleted = false;
            changed = true;
        }

        if (changed)
            plan.UpdatedAt = DateTime.UtcNow;

        return changed;
    }

    private static SubscriptionPlanResponse mapPlan(SubscriptionPlan plan)
    {
        var definition = getManagedPlanDefinition(plan.Name);
        return new SubscriptionPlanResponse
        {
            Id = plan.Id,
            Code = definition?.Code ?? plan.Name.ToUpperInvariant(),
            Name = plan.Name,
            Description = plan.Description,
            Price = plan.Price,
            DurationDays = plan.DurationDays,
            Features = definition?.Features ?? splitFeatures(plan.Features),
            SortOrder = plan.SortOrder,
            Benefits = definition?.Benefits ?? new SubscriptionBenefitResponse()
        };
    }

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
            : tryParseJsonFeatures(features) ?? features
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

    private static ManagedPlanDefinition? getManagedPlanDefinition(string? planName) =>
        ManagedPlans.FirstOrDefault(p => string.Equals(p.Name, planName, StringComparison.OrdinalIgnoreCase));

    private static List<string>? tryParseJsonFeatures(string features)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(features);
        }
        catch
        {
            return null;
        }
    }

}
