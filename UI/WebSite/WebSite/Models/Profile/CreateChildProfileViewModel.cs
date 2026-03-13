namespace WebSite.Models.Profile
{
    public class CreateChildProfileViewModel
    {
        public string? Name { get; set; } = null!;
        public DateOnly DateOfBirth { get; set; }
        public int? Gender { get; set; }
        public string? SpecialNeeds { get; set; }
        public string? Allergies { get; set; }
        public string? Notes { get; set; }
    }
}
