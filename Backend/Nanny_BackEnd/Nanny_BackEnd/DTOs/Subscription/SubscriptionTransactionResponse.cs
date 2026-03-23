namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionTransactionResponse
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public string StatusLabel { get; set; } = "";
    public int Type { get; set; }
    public string TypeLabel { get; set; } = "";
    public string? Description { get; set; }
    public string? PaymentGatewayTransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
