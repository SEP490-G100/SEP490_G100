namespace WebSite.Models.Profile
{
    public class ParentOnboardingWizardViewModel
    {
        // Bước 1
        public string? FullName { get; set; }

        // Bước 2
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Bước 3
        public string? AvatarUrl { get; set; }

        // Bước 4
        public string? FamilyDescription { get; set; }
        public int? NumberOfChildren { get; set; }

        // Bước 5 - Child
        public System.DateOnly? ChildDateOfBirth { get; set; }
        public string? ChildSpecialNeeds { get; set; }
        public string? ChildAllergies { get; set; }
        public string? ChildNotes { get; set; }
    }
}

