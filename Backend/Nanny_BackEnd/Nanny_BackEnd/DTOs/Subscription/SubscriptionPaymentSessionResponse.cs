namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionPaymentSessionResponse
{
    public Guid TransactionId { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string PlanName { get; set; } = "";
    public decimal Amount { get; set; }
    public int OrderCode { get; set; }
    public string PaymentContent { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
    public string QrCodeUrl { get; set; } = "";
    public string BankId { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string ProviderPaymentId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
