namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionPlanResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public List<string> Features { get; set; } = [];
    public int SortOrder { get; set; }
    public SubscriptionBenefitResponse Benefits { get; set; } = new();
}
