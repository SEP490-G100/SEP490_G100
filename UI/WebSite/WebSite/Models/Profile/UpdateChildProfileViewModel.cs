namespace WebSite.Models.Profile
{
    using WebSite.Enums;

    public class UpdateChildProfileViewModel
    {
        public Guid Id { get; set; }
        public string? SpecialNeeds { get; set; }
        public string? Notes { get; set; }
        public string? Characteristic { get; set; }
        public ChildAgeGroup? ChildAgeGroup { get; set; }
    }
}
