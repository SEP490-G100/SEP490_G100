using WebSite.Models.Nanny;

namespace WebSite.Models.Home;

public class HomePageViewModel
{
    public List<NannyListItemViewModel> FeaturedNannies { get; set; } = new();
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
}
