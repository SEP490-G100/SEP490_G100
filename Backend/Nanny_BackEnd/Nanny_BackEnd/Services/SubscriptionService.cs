using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
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
            "Ho so duoc hien thi tot hon tai khoan Free"
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
    private readonly VietQrOptions _vietQrOptions;
    private readonly VnPayService _vnPayService;

    public SubscriptionService(
        SubscriptionRepository subscriptionRepo,
        NotificationService notificationService,
        VietQrService vietQrService,
        IOptions<VietQrOptions> vietQrOptions,
        VnPayService vnPayService)
    {
        _subscriptionRepo = subscriptionRepo;
        _notificationService = notificationService;
        _vietQrService = vietQrService;
        _vietQrOptions = vietQrOptions.Value;
        _vnPayService = vnPayService;
    }

    public async Task<List<SubscriptionPlanResponse>> getPlans()
    {
        var plans = await ensureManagedPlans();
        return plans.Select(mapPlan).ToList();
    }

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

    public async Task<List<SubscriptionTransactionResponse>> getTransactionHistory(Guid userId)
    {
        await expirePendingTransactions(userId);
        var transactions = await _subscriptionRepo.getUserSubscriptionTransactions(userId);
        return transactions.Select(mapTransaction).ToList();
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
        await expirePendingTransactions(userId);
        await ensureManagedPlans();

        var plan = await _subscriptionRepo.findPlanById(request.SubscriptionPlanId)
            ?? throw new KeyNotFoundException("Khong tim thay goi subscription hoac goi da ngung hoat dong.");

        var definition = getManagedPlanDefinition(plan.Name)
            ?? throw new InvalidOperationException("He thong chi ho tro cac goi subscription duoc quan ly san.");

        await validatePlanOwnership(userId, definition);

        var currentSubscription = await _subscriptionRepo.findCurrentSubscription(userId, DateTime.UtcNow);
        if (currentSubscription != null)
            throw new InvalidOperationException("Ban dang co goi subscription con hieu luc. Vui long huy hoac cho goi hien tai het han.");

        var existingPendingTransaction = await findReusablePendingTransaction(userId, definition.Code);
        if (existingPendingTransaction != null)
            return buildPaymentSessionResponse(plan, existingPendingTransaction);

        var nowUtc = DateTime.UtcNow;
        var orderCode = await generateUniqueOrderCode();
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
            return buildPaymentSessionResponse(plan, transaction);
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

        if (transaction.Status == 1 && isPendingTransactionExpired(transaction, DateTime.UtcNow))
        {
            markTransactionFailed(transaction, DateTime.UtcNow);
            await _subscriptionRepo.saveChanges();
        }

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

    public async Task<SubscriptionQrCallbackResponse> handleQrCallback(SubscriptionQrCallbackRequest request)
    {
        var result = await confirmTransfer(
            request.OrderCode.ToString(),
            request.Amount,
            request.IsSuccess,
            request.ProviderTransactionId,
            request.TransferContent);

        return new SubscriptionQrCallbackResponse
        {
            Processed = result.IsSuccess,
            SubscriptionActivated = result.SubscriptionActivated,
            TransactionId = result.TransactionId,
            Status = result.Status,
            Message = result.Message
        };
    }

    public async Task<VnPayReturnResult> handleVnPayReturn(IQueryCollection query)
    {
        var process = await processVnPayCallback(query);
        var transactionId = process.TransactionId ?? Guid.Empty;
        var redirectUrl = process.IsSuccess
            ? _vnPayService.buildSuccessUrl(transactionId)
            : _vnPayService.buildCancelUrl(transactionId);

        return new VnPayReturnResult
        {
            TransactionId = process.TransactionId,
            IsSuccess = process.IsSuccess,
            Message = process.Message,
            RedirectUrl = redirectUrl
        };
    }

    public async Task<VnPayIpnResult> handleVnPayIpn(IQueryCollection query)
    {
        try
        {
            var process = await processVnPayCallback(query);
            if (!process.IsValidSignature)
                return new VnPayIpnResult { RspCode = "97", Message = "Invalid signature" };

            if (!process.TransactionFound)
                return new VnPayIpnResult { RspCode = "01", Message = "Order not found" };

            if (process.AmountMismatch)
                return new VnPayIpnResult { RspCode = "04", Message = "Invalid amount" };

            if (process.IsAlreadyProcessed)
                return new VnPayIpnResult { RspCode = "02", Message = "Order already confirmed" };

            return new VnPayIpnResult { RspCode = "00", Message = "Confirm success" };
        }
        catch
        {
            return new VnPayIpnResult { RspCode = "99", Message = "Unknown error" };
        }
    }

    // Backward compatibility for existing tests and legacy webhook flow.
    public async Task<int> handleVietQrWebhook(VietQrWebhookRequest request)
    {
        if (request.Data.Count == 0)
            return 0;

        var processed = 0;
        foreach (var item in request.Data)
        {
            if (item.OrderCode <= 0)
                continue;

            var result = await confirmTransfer(
                item.OrderCode.ToString(),
                item.Amount,
                string.Equals(item.Code, "00", StringComparison.OrdinalIgnoreCase),
                item.Reference ?? item.PaymentLinkId,
                item.Description);

            if (result.IsSuccess)
                processed++;
        }

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

    public async Task<bool> hasActiveParentSubscription(Guid parentProfileId)
    {
        await ensureManagedPlans();
        var subscription = await _subscriptionRepo.findCurrentSubscriptionByParentProfile(parentProfileId, DateTime.UtcNow);
        var definition = getManagedPlanDefinition(subscription?.SubscriptionPlan?.Name);
        return definition != null && string.Equals(definition.TargetRole, "Parent", StringComparison.OrdinalIgnoreCase);
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

    private async Task<Transaction?> findReusablePendingTransaction(Guid userId, string planCode)
    {
        var nowUtc = DateTime.UtcNow;
        var pendingTransactions = await _subscriptionRepo.getPendingSubscriptionTransactions(userId);
        if (pendingTransactions.Count == 0)
            return null;

        Transaction? reusable = null;
        var hasChanges = false;

        foreach (var transaction in pendingTransactions)
        {
            if (isPendingTransactionExpired(transaction, nowUtc))
            {
                markTransactionFailed(transaction, nowUtc);
                hasChanges = true;
                continue;
            }

            if (reusable == null &&
                string.Equals(tryExtractPlanCode(transaction.Description), planCode, StringComparison.OrdinalIgnoreCase))
            {
                reusable = transaction;
            }
        }

        if (hasChanges)
            await _subscriptionRepo.saveChanges();

        return reusable;
    }

    private SubscriptionPaymentSessionResponse buildPaymentSessionResponse(SubscriptionPlan plan, Transaction transaction)
    {
        var orderCode = int.TryParse(transaction.PaymentGatewayTransactionId, out var parsedOrderCode)
            ? parsedOrderCode
            : 0;

        var paymentInstruction = _vietQrService.createPaymentInstruction(
            orderCode,
            transaction.Amount,
            transaction.Description ?? buildPaymentContent(getManagedPlanDefinition(plan.Name)?.Code ?? plan.Name, orderCode));

        return new SubscriptionPaymentSessionResponse
        {
            TransactionId = transaction.Id,
            PaymentMethod = "BANK_QR",
            PlanName = plan.Name,
            Amount = transaction.Amount,
            OrderCode = paymentInstruction.OrderCode,
            PaymentContent = paymentInstruction.TransferContent,
            CheckoutUrl = paymentInstruction.CheckoutUrl,
            QrCodeUrl = paymentInstruction.QrCodeUrl,
            BankId = paymentInstruction.BankId,
            AccountNumber = paymentInstruction.AccountNumber,
            AccountName = paymentInstruction.AccountName,
            ProviderPaymentId = transaction.PaymentGatewayTransactionId ?? "",
            Status = "PENDING",
            ExpiresAt = getPendingTransactionExpiresAt(transaction.CreatedAt)
        };
    }

    private async Task expirePendingTransactions(Guid userId)
    {
        var nowUtc = DateTime.UtcNow;
        var pendingTransactions = await _subscriptionRepo.getPendingSubscriptionTransactions(userId);
        if (pendingTransactions.Count == 0)
            return;

        var hasChanges = false;
        foreach (var transaction in pendingTransactions)
        {
            if (!isPendingTransactionExpired(transaction, nowUtc))
                continue;

            markTransactionFailed(transaction, nowUtc);
            hasChanges = true;
        }

        if (hasChanges)
            await _subscriptionRepo.saveChanges();
    }

    private bool isPendingTransactionExpired(Transaction transaction, DateTime nowUtc) =>
        transaction.Status == 1 && getPendingTransactionExpiresAt(transaction.CreatedAt) <= nowUtc;

    private DateTime getPendingTransactionExpiresAt(DateTime createdAtUtc) =>
        createdAtUtc.AddMinutes(Math.Max(1, _vietQrOptions.ExpiresAfterMinutes));

    private async Task<TransferConfirmationResult> confirmTransfer(
        string gatewayCode,
        decimal amount,
        bool isSuccess,
        string? providerTransactionId = null,
        string? transferContent = null)
    {
        var transaction = await _subscriptionRepo.findTransactionByGatewayCode(gatewayCode);
        if (transaction == null)
        {
            return new TransferConfirmationResult
            {
                Status = "NOT_FOUND",
                Message = "Khong tim thay giao dich thanh toan."
            };
        }

        var result = new TransferConfirmationResult
        {
            TransactionId = transaction.Id
        };

        if (await _subscriptionRepo.hasAnySubscriptionLinkedToTransaction(transaction.Id) || transaction.Status == 2)
        {
            result.IsSuccess = true;
            result.SubscriptionActivated = true;
            result.Status = "ALREADY_PROCESSED";
            result.Message = "Giao dich da duoc xu ly truoc do.";
            return result;
        }

        if (transaction.Status == 3)
        {
            result.Status = "FAILED";
            result.Message = "Giao dich da o trang thai that bai.";
            return result;
        }

        var nowUtc = DateTime.UtcNow;
        if (!isSuccess)
        {
            markTransactionFailed(transaction, nowUtc);
            await _subscriptionRepo.saveChanges();

            result.Status = "FAILED";
            result.Message = "Callback noi bo tra ve trang thai that bai.";
            return result;
        }

        var expectedAmount = decimal.Round(transaction.Amount, 0, MidpointRounding.AwayFromZero);
        var callbackAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        if (callbackAmount <= 0 || callbackAmount != expectedAmount)
        {
            markTransactionFailed(transaction, nowUtc);
            await _subscriptionRepo.saveChanges();

            result.Status = "AMOUNT_MISMATCH";
            result.Message = "So tien callback khong khop voi giao dich dang cho.";
            return result;
        }

        transaction.Status = 2;
        transaction.CompletedAt = nowUtc;
        transaction.UpdatedAt = nowUtc;
        transaction.UpdatedBy = transaction.UserId;
        transaction.Description = appendCallbackMetadata(transaction.Description, transferContent, providerTransactionId);

        var activated = await activateSubscriptionFromTransaction(transaction, nowUtc);
        if (!activated)
        {
            markTransactionFailed(transaction, nowUtc);
            await _subscriptionRepo.saveChanges();

            result.Status = "ACTIVATION_FAILED";
            result.Message = "Khong kich hoat duoc goi subscription tu callback noi bo.";
            return result;
        }

        await _subscriptionRepo.saveChanges();

        result.IsSuccess = true;
        result.SubscriptionActivated = true;
        result.Status = "SUCCESS";
        result.Message = "Da kich hoat subscription tu callback noi bo.";
        return result;
    }

    private async Task<VnPayProcessInternalResult> processVnPayCallback(IQueryCollection query)
    {
        var callback = _vnPayService.validateCallback(query);
        if (!callback.IsValidSignature)
        {
            return new VnPayProcessInternalResult
            {
                IsValidSignature = false,
                Message = callback.Message
            };
        }

        if (string.IsNullOrWhiteSpace(callback.OrderCode))
        {
            return new VnPayProcessInternalResult
            {
                IsValidSignature = true,
                Message = "Khong tim thay ma don hang VNPay."
            };
        }

        var transaction = await _subscriptionRepo.findTransactionByGatewayCode(callback.OrderCode);
        if (transaction == null)
        {
            return new VnPayProcessInternalResult
            {
                IsValidSignature = true,
                TransactionFound = false,
                Message = "Khong tim thay giao dich thanh toan."
            };
        }

        var result = new VnPayProcessInternalResult
        {
            IsValidSignature = true,
            TransactionFound = true,
            TransactionId = transaction.Id
        };

        var linkedSubscription = await _subscriptionRepo.hasAnySubscriptionLinkedToTransaction(transaction.Id);
        if (linkedSubscription || transaction.Status == 2)
        {
            result.IsAlreadyProcessed = true;
            result.IsSuccess = true;
            result.Message = "Giao dich da duoc xu ly truoc do.";
            return result;
        }

        if (transaction.Status == 3)
        {
            result.IsAlreadyProcessed = true;
            result.IsSuccess = false;
            result.Message = "Giao dich da o trang thai that bai.";
            return result;
        }

        var expectedAmount = decimal.Round(transaction.Amount, 0, MidpointRounding.AwayFromZero);
        var callbackAmount = decimal.Round(callback.Amount, 0, MidpointRounding.AwayFromZero);
        if (callbackAmount <= 0 || callbackAmount != expectedAmount)
        {
            markTransactionFailed(transaction, DateTime.UtcNow);
            await _subscriptionRepo.saveChanges();

            result.AmountMismatch = true;
            result.IsSuccess = false;
            result.Message = "So tien giao dich khong khop.";
            return result;
        }

        if (!callback.IsSuccess)
        {
            markTransactionFailed(transaction, DateTime.UtcNow);
            await _subscriptionRepo.saveChanges();

            result.IsSuccess = false;
            result.Message = "VNPay tra ve trang thai thanh toan that bai.";
            return result;
        }

        var nowUtc = DateTime.UtcNow;
        transaction.Status = 2;
        transaction.CompletedAt = nowUtc;
        transaction.UpdatedAt = nowUtc;
        transaction.UpdatedBy = transaction.UserId;

        var activated = await activateSubscriptionFromTransaction(transaction, nowUtc);
        if (!activated)
        {
            markTransactionFailed(transaction, nowUtc);
            await _subscriptionRepo.saveChanges();

            result.IsSuccess = false;
            result.Message = "Khong kich hoat duoc goi subscription tu giao dich thanh toan.";
            return result;
        }

        await _subscriptionRepo.saveChanges();
        result.IsSuccess = true;
        result.Message = "Da xu ly callback VNPay thanh cong.";
        return result;
    }

    private async Task<bool> activateSubscriptionFromTransaction(Transaction transaction, DateTime nowUtc)
    {
        if (await _subscriptionRepo.hasAnySubscriptionLinkedToTransaction(transaction.Id))
            return true;

        var planCode = tryExtractPlanCode(transaction.Description);
        if (string.IsNullOrWhiteSpace(planCode))
            return false;

        var planDto = await getPlanByCode(planCode);
        if (planDto == null)
            return false;

        var plan = await _subscriptionRepo.findPlanById(planDto.Id);
        if (plan == null)
            return false;

        var currentSubscription = await _subscriptionRepo.findCurrentSubscription(transaction.UserId, nowUtc);
        if (currentSubscription != null)
            return false;

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

        return true;
    }

    private static void markTransactionFailed(Transaction transaction, DateTime nowUtc)
    {
        transaction.Status = 3;
        transaction.UpdatedAt = nowUtc;
        transaction.UpdatedBy = transaction.UserId;
    }

    private static string appendCallbackMetadata(
        string? description,
        string? transferContent,
        string? providerTransactionId)
    {
        var baseDescription = description?.Trim() ?? "";
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(baseDescription))
            segments.Add(baseDescription);

        if (!string.IsNullOrWhiteSpace(transferContent) &&
            !string.Equals(transferContent.Trim(), baseDescription, StringComparison.OrdinalIgnoreCase))
        {
            segments.Add($"TRANSFER:{transferContent.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(providerTransactionId))
            segments.Add($"REF:{providerTransactionId.Trim()}");

        return string.Join(" | ", segments);
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

    private async Task<int> generateUniqueOrderCode()
    {
        const int maxAttempts = 8;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = generateOrderCode();
            if (!await _subscriptionRepo.existsGatewayTransactionCode(code.ToString()))
                return code;

            await Task.Delay(5);
        }

        throw new InvalidOperationException("Khong tao duoc ma giao dich thanh toan duy nhat. Vui long thu lai.");
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

    private sealed class VnPayProcessInternalResult
    {
        public bool IsValidSignature { get; set; }
        public bool TransactionFound { get; set; }
        public bool IsAlreadyProcessed { get; set; }
        public bool AmountMismatch { get; set; }
        public bool IsSuccess { get; set; }
        public Guid? TransactionId { get; set; }
        public string Message { get; set; } = "";
    }

    private sealed class TransferConfirmationResult
    {
        public bool IsSuccess { get; set; }
        public bool SubscriptionActivated { get; set; }
        public Guid? TransactionId { get; set; }
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
    }
}

public class VnPayReturnResult
{
    public Guid? TransactionId { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
    public string RedirectUrl { get; set; } = "";
}

public class VnPayIpnResult
{
    public string RspCode { get; set; } = "99";
    public string Message { get; set; } = "Unknown error";
}
