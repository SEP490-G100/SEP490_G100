using System.Text.Json.Serialization;

namespace WebSite.Models.Subscription;

public class SubscriptionBenefitViewModel
{
    [JsonPropertyName("monthlyJobPostLimit")]
    public int MonthlyJobPostLimit { get; set; }

    [JsonPropertyName("monthlyApplicationLimit")]
    public int MonthlyApplicationLimit { get; set; }

    [JsonPropertyName("featuredBadge")]
    public bool FeaturedBadge { get; set; }

    [JsonPropertyName("searchPriority")]
    public bool SearchPriority { get; set; }

    [JsonPropertyName("listingDurationDays")]
    public int ListingDurationDays { get; set; }
}

public class SubscriptionPlanViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("targetRole")]
    public string TargetRole { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("durationDays")]
    public int DurationDays { get; set; }

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = [];

    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    [JsonPropertyName("benefits")]
    public SubscriptionBenefitViewModel Benefits { get; set; } = new();
}

public class SubscriptionTransactionViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("statusLabel")]
    public string StatusLabel { get; set; } = "";

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("typeLabel")]
    public string TypeLabel { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("paymentGatewayTransactionId")]
    public string? PaymentGatewayTransactionId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}

public class UserSubscriptionViewModel
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("statusLabel")]
    public string StatusLabel { get; set; } = "";

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime EndDate { get; set; }

    [JsonPropertyName("cancelledAt")]
    public DateTime? CancelledAt { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("remainingDays")]
    public int RemainingDays { get; set; }

    [JsonPropertyName("plan")]
    public SubscriptionPlanViewModel Plan { get; set; } = new();

    [JsonPropertyName("transaction")]
    public SubscriptionTransactionViewModel? Transaction { get; set; }
}

public class SubscriptionPaymentSessionViewModel
{
    [JsonPropertyName("transactionId")]
    public Guid TransactionId { get; set; }

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = "";

    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = "";

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("orderCode")]
    public int OrderCode { get; set; }

    [JsonPropertyName("paymentContent")]
    public string PaymentContent { get; set; } = "";

    [JsonPropertyName("checkoutUrl")]
    public string CheckoutUrl { get; set; } = "";

    [JsonPropertyName("qrCodeUrl")]
    public string QrCodeUrl { get; set; } = "";

    [JsonPropertyName("bankId")]
    public string BankId { get; set; } = "";

    [JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; } = "";

    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } = "";

    [JsonPropertyName("providerPaymentId")]
    public string ProviderPaymentId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

public class SubscriptionPaymentStatusViewModel
{
    [JsonPropertyName("transactionId")]
    public Guid TransactionId { get; set; }

    [JsonPropertyName("transactionStatus")]
    public int TransactionStatus { get; set; }

    [JsonPropertyName("transactionStatusLabel")]
    public string TransactionStatusLabel { get; set; } = "";

    [JsonPropertyName("planName")]
    public string PlanName { get; set; } = "";

    [JsonPropertyName("subscriptionActivated")]
    public bool SubscriptionActivated { get; set; }
}

public class SubscriptionPaymentResultPageViewModel
{
    public Guid TransactionId { get; set; }
    public bool Cancelled { get; set; }
    public SubscriptionPaymentStatusViewModel? PaymentStatus { get; set; }
}

public class SubscriptionPageViewModel
{
    public string CurrentRole { get; set; } = "Guest";
    public string RoleLabel { get; set; } = "Nguoi dung";
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public SubscriptionBenefitViewModel FreeBenefits { get; set; } = new();
    public List<string> FreeFeatures { get; set; } = [];
    public List<SubscriptionPlanViewModel> Plans { get; set; } = [];
    public List<SubscriptionTransactionViewModel> Transactions { get; set; } = [];
    public UserSubscriptionViewModel? CurrentSubscription { get; set; }
    public bool HasActiveSubscription => CurrentSubscription?.IsActive == true;
    public bool IsParent => string.Equals(CurrentRole, "Parent", StringComparison.OrdinalIgnoreCase);
    public bool IsNanny => string.Equals(CurrentRole, "Nanny", StringComparison.OrdinalIgnoreCase);
    public bool SupportsSubscription => IsParent || IsNanny;
}
