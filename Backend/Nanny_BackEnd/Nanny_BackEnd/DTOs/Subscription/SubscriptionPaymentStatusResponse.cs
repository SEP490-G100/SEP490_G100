namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionPaymentStatusResponse
{
    public Guid TransactionId { get; set; }
    public int TransactionStatus { get; set; }
    public string TransactionStatusLabel { get; set; } = "";
    public string PlanName { get; set; } = "";
    public bool SubscriptionActivated { get; set; }
    public string? CheckoutUrl { get; set; }
}
