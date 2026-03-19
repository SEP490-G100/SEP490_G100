using System.Text.Json;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class SubscriptionService
{
    private sealed record ManagedPlanDefinition(
        string Code,
        string TargetRole,
        string Name,
        string Description,
        decimal Price,
        int DurationDays,
        int SortOrder,
        SubscriptionBenefitResponse Benefits,
        List<string> Features);

    private static readonly ManagedPlanDefinition ParentPlusPlan = new(
        Code: "PLUS",
        TargetRole: "Parent",
        Name: "Plus",
        Description: "Goi Plus cho Parent can dang them bai va lam bai dang noi bat hon.",
        Price: 299000m,
        DurationDays: 30,
        SortOrder: 1,
        Benefits: new SubscriptionBenefitResponse
        {
            MonthlyJobPostLimit = 3,
            MonthlyApplicationLimit = 0,
            FeaturedBadge = true,
            SearchPriority = false,
            ListingDurationDays = 45
        },
        Features:
        [
            "Dang toi da 3 bai moi moi thang",
            "Bai dang co badge noi bat",
            "Thoi gian hien thi bai dang 45 ngay"
        ]);

    private static readonly ManagedPlanDefinition ParentProPlan = new(
        Code: "PRO",
        TargetRole: "Parent",
        Name: "Pro",
        Description: "Goi Pro cho Parent can uu tien hien thi va gia tang co hoi tiep can.",
        Price: 499000m,
        DurationDays: 30,
        SortOrder: 2,
        Benefits: new SubscriptionBenefitResponse
        {
            MonthlyJobPostLimit = 5,
            MonthlyApplicationLimit = 0,
            FeaturedBadge = true,
            SearchPriority = true,
            ListingDurationDays = 60
        },
        Features:
        [
            "Dang toi da 5 bai moi moi thang",
            "Bai dang co badge noi bat",
            "Duoc uu tien hien thi trong ket qua tim kiem",
            "Thoi gian hien thi bai dang 60 ngay"
        ]);

    private static readonly ManagedPlanDefinition NannyPlusPlan = new(
        Code: "NANNY_PLUS",
        TargetRole: "Nanny",
        Name: "Nanny Plus",
        Description: "Goi Plus cho Nanny muon co them luot ung tuyen va ho so noi bat hon.",
        Price: 199000m,
        DurationDays: 30,
        SortOrder: 3,
        Benefits: new SubscriptionBenefitResponse
        {
            MonthlyJobPostLimit = 0,
            MonthlyApplicationLimit = 3,
            FeaturedBadge = true,
            SearchPriority = false,
            ListingDurationDays = 0
        },
        Features:
        [
            "Ung tuyen toi da 3 cong viec moi moi thang",
            "Ho so co badge noi bat",
            "Ho so duoc hien thi tot hon tai khoan free"
        ]);

    private static readonly ManagedPlanDefinition NannyProPlan = new(
        Code: "NANNY_PRO",
        TargetRole: "Nanny",
        Name: "Nanny Pro",
        Description: "Goi Pro cho Nanny muon co them luot ung tuyen va uu tien hien thi cao hon.",
        Price: 299000m,
        DurationDays: 30,
        SortOrder: 4,
        Benefits: new SubscriptionBenefitResponse
        {
            MonthlyJobPostLimit = 0,
            MonthlyApplicationLimit = 5,
            FeaturedBadge = true,
            SearchPriority = true,
            ListingDurationDays = 0
        },
        Features:
        [
            "Ung tuyen toi da 5 cong viec moi moi thang",
            "Ho so co badge noi bat",
            "Ho so duoc uu tien hien thi cao hon goi Nanny Plus"
        ]);

    private static readonly ManagedPlanDefinition[] ManagedPlans =
    [
        ParentPlusPlan,
        ParentProPlan,
        NannyPlusPlan,
        NannyProPlan
    ];

    private readonly SubscriptionRepository _subscriptionRepo;
    private readonly NotificationService _notificationService;
    private readonly VietQrService _vietQrService;

    public SubscriptionService(
        SubscriptionRepository subscriptionRepo,
        NotificationService notificationService,
        VietQrService vietQrService)
    {
        _subscriptionRepo = subscriptionRepo;
        _notificationService = notificationService;
        _vietQrService = vietQrService;
    }

    public async Task<List<SubscriptionPlanResponse>> getPlans()
    {
        var plans = await ensureManagedPlans();
        return plans.Select(mapPlan).ToList();
    }

    public bool isVietQrWebhookAuthorized(string? webhookToken) =>
        _vietQrService.isWebhookAuthorized(webhookToken);

    public async Task<SubscriptionPlanResponse?> getPlanByCode(string code)
    {
        var plans = await ensureManagedPlans();
        var plan = plans.FirstOrDefault(p =>
            string.Equals(getManagedPlanDefinition(p.Name)?.Code, code, StringComparison.OrdinalIgnoreCase));
        return plan == null ? null : mapPlan(plan);
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
            ?? throw new KeyNotFoundException("Khong tim thay goi subscription hoac goi da ngung hoat dong.");

        var definition = getManagedPlanDefinition(plan.Name)
            ?? throw new InvalidOperationException("He thong chi ho tro cac goi subscription duoc quan ly san.");

        await validatePlanOwnership(userId, definition);

        var currentSubscription = await _subscriptionRepo.findCurrentSubscription(userId, DateTime.UtcNow);
        if (currentSubscription != null)
            throw new InvalidOperationException("Ban dang co goi subscription con hieu luc. Vui long huy hoac cho goi hien tai het han.");

        var nowUtc = DateTime.UtcNow;
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = plan.Price,
            PaymentGatewayTransactionId = request.PaymentGatewayTransactionId?.Trim(),
            Status = 2,
            Description = $"Thanh toan goi {plan.Name}",
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

    public async Task<SubscriptionPaymentSessionResponse> createPayment(Guid userId, CreateSubscriptionPaymentRequest request)
    {
        await expireOldSubscriptions(userId);
        await ensureManagedPlans();

        var plan = await _subscriptionRepo.findPlanById(request.SubscriptionPlanId)
            ?? throw new KeyNotFoundException("Khong tim thay goi subscription hoac goi da ngung hoat dong.");

        var definition = getManagedPlanDefinition(plan.Name)
            ?? throw new InvalidOperationException("He thong chi ho tro cac goi subscription duoc quan ly san.");

        await validatePlanOwnership(userId, definition);

        var currentSubscription = await _subscriptionRepo.findCurrentSubscription(userId, DateTime.UtcNow);
        if (currentSubscription != null)
            throw new InvalidOperationException("Ban dang co goi subscription con hieu luc. Vui long huy hoac cho goi hien tai het han.");

        var user = (await _subscriptionRepo.getUsersByIds([userId])).FirstOrDefault()
            ?? throw new KeyNotFoundException("Khong tim thay nguoi dung hien tai.");

        var nowUtc = DateTime.UtcNow;
        var orderCode = generateOrderCode();
        var paymentContent = buildPaymentContent(definition.Code, orderCode);
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = plan.Price,
            PaymentGatewayTransactionId = orderCode.ToString(),
            Status = 1,
            Description = paymentContent,
            Type = 1,
            CreatedAt = nowUtc,
            CreatedBy = userId
        };

        _subscriptionRepo.addTransaction(transaction);
        await _subscriptionRepo.saveChanges();

        try
        {
            var payment = await _vietQrService.createPayment(
                orderCode,
                plan.Price,
                paymentContent,
                getDisplayName(user),
                user.Email,
                _vietQrService.buildSuccessUrl(transaction.Id),
                _vietQrService.buildCancelUrl(transaction.Id));

            return new SubscriptionPaymentSessionResponse
            {
                TransactionId = transaction.Id,
                PlanName = plan.Name,
                Amount = plan.Price,
                OrderCode = orderCode,
                PaymentContent = paymentContent,
                CheckoutUrl = payment.Data!.CheckoutUrl,
                ProviderPaymentId = payment.Data.Id,
                Status = payment.Data.Status
            };
        }
        catch
        {
            transaction.Status = 3;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.UpdatedBy = userId;
            await _subscriptionRepo.saveChanges();
            throw;
        }
    }

    public async Task<SubscriptionPaymentStatusResponse> getPaymentStatus(Guid userId, Guid transactionId)
    {
        var transaction = await _subscriptionRepo.findTransactionById(transactionId, userId)
            ?? throw new KeyNotFoundException("Khong tim thay giao dich thanh toan.");

        var planCode = tryExtractPlanCode(transaction.Description);
        var planName = planCode == null
            ? "Subscription"
            : (await getPlanByCode(planCode))?.Name ?? "Subscription";

        return new SubscriptionPaymentStatusResponse
        {
            TransactionId = transaction.Id,
            TransactionStatus = transaction.Status,
            TransactionStatusLabel = getTransactionStatusLabel(transaction.Status),
            PlanName = planName,
            SubscriptionActivated = await _subscriptionRepo.hasAnySubscriptionLinkedToTransaction(transaction.Id)
        };
    }

    public async Task<int> handleVietQrWebhook(VietQrWebhookRequest request)
    {
        if (request.Data.Count == 0)
            return 0;

        var processed = 0;

        foreach (var item in request.Data)
        {
            if (item.OrderCode <= 0)
                continue;

            var transaction = await _subscriptionRepo.findTransactionByGatewayCode(item.OrderCode.ToString());
            if (transaction == null)
                continue;

            if (!string.IsNullOrWhiteSpace(item.Code) &&
                !string.Equals(item.Code, "00", StringComparison.OrdinalIgnoreCase))
            {
                transaction.Status = 3;
                transaction.UpdatedAt = DateTime.UtcNow;
                continue;
            }

            if (transaction.Status == 2 || await _subscriptionRepo.hasAnySubscriptionLinkedToTransaction(transaction.Id))
                continue;

            var planCode = tryExtractPlanCode(transaction.Description)
                ?? throw new InvalidOperationException("Khong xac dinh duoc goi tu giao dich VietQR.");

            var planDto = await getPlanByCode(planCode)
                ?? throw new KeyNotFoundException("Khong tim thay goi subscription can kich hoat.");

            var plan = await _subscriptionRepo.findPlanById(planDto.Id)
                ?? throw new KeyNotFoundException("Khong tim thay goi subscription dang hoat dong.");

            var nowUtc = DateTime.UtcNow;
            transaction.Status = 2;
            transaction.CompletedAt = nowUtc;
            transaction.UpdatedAt = nowUtc;

            var subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = transaction.UserId,
                SubscriptionPlanId = plan.Id,
                StartDate = nowUtc,
                EndDate = nowUtc.AddDays(plan.DurationDays),
                Status = 1,
                PaymentTransactionId = transaction.Id,
                CreatedAt = nowUtc,
                CreatedBy = transaction.UserId
            };

            _subscriptionRepo.addUserSubscription(subscription);

            await _notificationService.createNotification(
                transaction.UserId,
                $"Dang ky goi {plan.Name} thanh cong",
                $"Ban da thanh toan thanh cong goi {plan.Name}. Goi cua ban co hieu luc den {subscription.EndDate:dd/MM/yyyy}.",
                NotificationTypes.SubscriptionPurchased,
                subscription.Id,
                "UserSubscription",
                null);

            processed++;
        }

        await _subscriptionRepo.saveChanges();
        return processed;
    }

    public async Task<SubscriptionBenefitResponse> getBenefitsForParentProfile(Guid parentProfileId)
    {
        await ensureManagedPlans();
        var subscription = await _subscriptionRepo.findCurrentSubscriptionByParentProfile(parentProfileId, DateTime.UtcNow);
        var definition = getManagedPlanDefinition(subscription?.SubscriptionPlan?.Name);
        return definition?.Benefits ?? SubscriptionBenefitResponse.FreeParent;
    }

    public async Task<SubscriptionBenefitResponse> getBenefitsForNannyProfile(Guid nannyProfileId)
    {
        await ensureManagedPlans();
        var subscription = await _subscriptionRepo.findCurrentSubscriptionByNannyProfile(nannyProfileId, DateTime.UtcNow);
        var definition = getManagedPlanDefinition(subscription?.SubscriptionPlan?.Name);
        return definition?.Benefits ?? SubscriptionBenefitResponse.FreeNanny;
    }

    public async Task<UserSubscriptionResponse> cancelCurrentSubscription(Guid userId)
    {
        await expireOldSubscriptions(userId);

        var subscription = await _subscriptionRepo.findCurrentSubscription(userId, DateTime.UtcNow)
            ?? throw new KeyNotFoundException("Ban khong co goi subscription dang hoat dong.");

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
            Code = definition?.Code ?? plan.Name.ToUpperInvariant().Replace(' ', '_'),
            TargetRole = definition?.TargetRole ?? "Unknown",
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
        1 => "Dang hoat dong",
        2 => "Da huy",
        3 => "Het han",
        _ => "Khong xac dinh"
    };

    private static string getTransactionStatusLabel(int status) => status switch
    {
        1 => "Cho thanh toan",
        2 => "Thanh cong",
        3 => "That bai",
        4 => "Da hoan tien",
        _ => "Khong xac dinh"
    };

    private static string getTransactionTypeLabel(int type) => type switch
    {
        0 => "Nap tien",
        1 => "Thanh toan subscription",
        _ => "Khac"
    };

    private static int generateOrderCode()
    {
        var baseValue = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 900000000;
        return (int)(baseValue + 100000000);
    }

    private static string buildPaymentContent(string planCode, int orderCode) => $"NM {planCode} {orderCode}";

    private static string? tryExtractPlanCode(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        var parts = description.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 3 && string.Equals(parts[0], "NM", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : null;
    }

    private static string getDisplayName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

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

    private async Task validatePlanOwnership(Guid userId, ManagedPlanDefinition definition)
    {
        if (string.Equals(definition.TargetRole, "Parent", StringComparison.OrdinalIgnoreCase))
        {
            if (!await _subscriptionRepo.hasParentProfile(userId))
                throw new InvalidOperationException("Tai khoan hien tai khong phai Parent nen khong the mua goi nay.");

            return;
        }

        if (string.Equals(definition.TargetRole, "Nanny", StringComparison.OrdinalIgnoreCase))
        {
            if (!await _subscriptionRepo.hasNannyProfile(userId))
                throw new InvalidOperationException("Tai khoan hien tai khong phai Nanny nen khong the mua goi nay.");
        }
    }
}
