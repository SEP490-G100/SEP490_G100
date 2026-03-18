namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionPaymentSessionResponse
{
    public Guid TransactionId { get; set; }
    public string PlanName { get; set; } = "";
    public decimal Amount { get; set; }
    public int OrderCode { get; set; }
    public string PaymentContent { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
    public string ProviderPaymentId { get; set; } = "";
    public string Status { get; set; } = "";
}
