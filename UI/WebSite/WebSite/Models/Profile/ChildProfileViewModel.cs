namespace WebSite.Models.Profile
{
    using WebSite.Enums;

    public class ChildProfileViewModel
    {
        public Guid Id { get; set; }
        public Guid ParentProfileId { get; set; }
        public string? SpecialNeeds { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Characteristic { get; set; }
        public ChildAgeGroup? ChildAgeGroup { get; set; }
    }
}
