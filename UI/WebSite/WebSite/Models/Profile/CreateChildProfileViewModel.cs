namespace WebSite.Models.Profile
{
    using WebSite.Enums;

    public class CreateChildProfileViewModel
    {
        public string? SpecialNeeds { get; set; }
        public string? Notes { get; set; }
        public string? Characteristic { get; set; }
        public ChildAgeGroup? ChildAgeGroup { get; set; }
    }
}

