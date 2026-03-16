namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionBenefitResponse
{
    public int MonthlyJobPostLimit { get; set; }
    public bool FeaturedBadge { get; set; }
    public bool SearchPriority { get; set; }
    public int ListingDurationDays { get; set; }

    public static SubscriptionBenefitResponse Free => new()
    {
        MonthlyJobPostLimit = 2,
        FeaturedBadge = false,
        SearchPriority = false,
        ListingDurationDays = 30
    };
}
