namespace WebSite.Models.Profile
{
    public class ParentOnboardingWizardViewModel
    {
        // Step 1
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateOnly? DateOfBirth { get; set; }

        // Step 2
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Step 3
        public string? AvatarUrl { get; set; }
        public IFormFile? AvatarFile { get; set; }

        // Step 4
        public string? FamilyDescription { get; set; }
        public int? NumberOfChildren { get; set; }

        // Step 5 - Child
        public WebSite.Enums.ChildAgeGroup? ChildAgeGroup { get; set; }
        public string? ChildSpecialNeeds { get; set; }
        public string? ChildCharacteristic { get; set; }
        public string? ChildNotes { get; set; }
    }
}
