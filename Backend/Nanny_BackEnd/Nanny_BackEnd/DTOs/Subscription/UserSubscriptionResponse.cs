namespace Nanny_BackEnd.DTOs.Subscription;

public class UserSubscriptionResponse
{
    public Guid Id { get; set; }
    public int Status { get; set; }
    public string StatusLabel { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool IsActive { get; set; }
    public int RemainingDays { get; set; }
    public SubscriptionPlanResponse Plan { get; set; } = new();
    public SubscriptionTransactionResponse? Transaction { get; set; }
}
