namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionBenefitResponse
{
    public int MonthlyJobPostLimit { get; set; }
    public int MonthlyApplicationLimit { get; set; }
    public bool FeaturedBadge { get; set; }
    public bool SearchPriority { get; set; }
    public int ListingDurationDays { get; set; }
    public bool CanUseRecommendation { get; set; }

    public static SubscriptionBenefitResponse FreeParent => new()
    {
        MonthlyJobPostLimit = 1,
        MonthlyApplicationLimit = 0,
        FeaturedBadge = false,
        SearchPriority = false,
        ListingDurationDays = 7,
        CanUseRecommendation = false
    };

    public static SubscriptionBenefitResponse FreeNanny => new()
    {
        MonthlyJobPostLimit = 0,
        MonthlyApplicationLimit = 2,
        FeaturedBadge = false,
        SearchPriority = false,
        ListingDurationDays = 0,
        CanUseRecommendation = false
    };
}
