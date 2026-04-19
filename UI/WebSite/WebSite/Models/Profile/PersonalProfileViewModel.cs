namespace WebSite.Models.Profile
{
    public class PersonalProfileViewModel
    {
        public Guid UserId { get; set; }
        public Guid? NannyProfileId { get; set; }
        public Guid? ContactNannyProfileId { get; set; }
        public bool IsReadOnlyView { get; set; }
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        private int? _age;
        public int? Age
        {
            get => _age ?? CalculateAge(DateOfBirth);
            set => _age = value;
        }
        public int? Gender { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public List<string> Roles { get; set; } = new();

        public bool IsParent => Roles.Any(r => r.Equals("Parent", StringComparison.OrdinalIgnoreCase));
        public bool IsNanny => Roles.Any(r => r.Equals("Nanny", StringComparison.OrdinalIgnoreCase));
        // Parent
        public string? FamilyDescription { get; set; }
        public int? NumberOfChildren { get; set; }
        public string? SpecialNeeds { get; set; }
        public string? Notes { get; set; }
        public string? Characteristic { get; set; }
        public byte? ChildAgeGroup { get; set; }
        public List<ChildProfileViewModel>? Children { get; set; }

        // Nanny
        public string? Bio { get; set; }
        public int? YearsOfExperience { get; set; }
        public int? EducationLevel { get; set; }
        public decimal? ExpectedSalaryMin { get; set; }
        public decimal? ExpectedSalaryMax { get; set; }
        public int? MaxTravelDistance { get; set; }
        public string? VerificationStatus { get; set; }
        public int? VerificationStatusCode { get; set; }
        public bool HasHealthCertificate { get; set; }
        public decimal? AverageRating { get; set; }
        public int? TotalReviews { get; set; }
        public List<NannySkillItemViewModel>? Skills { get; set; }
        public List<NannyAvailabilityItemViewModel>? Availabilities { get; set; }
        public List<NannyCertificateItemViewModel>? Certificates { get; set; }
        public List<ReviewItemViewModel> Reviews { get; set; } = new();
        public int ReviewTotalCount { get; set; }

        public string VerificationStatusLabel =>
            string.IsNullOrWhiteSpace(VerificationStatus) ? "Chưa được xác thực" : VerificationStatus!;

        public string AverageRatingLabel =>
            AverageRating.HasValue ? AverageRating.Value.ToString("0.##") : "0";

        private static int? CalculateAge(DateOnly? dateOfBirth)
        {
            if (!dateOfBirth.HasValue)
                return null;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var dob = dateOfBirth.Value;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age))
                age--;

            return age > 0 ? age : null;
        }
    }

    public class ReviewItemViewModel
    {
        public Guid Id { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string ReviewerName { get; set; } = "";
        public string? ReviewerAvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Stars => new string('★', Rating) + new string('☆', 5 - Rating);
    }

    public class NannySkillItemViewModel
    {
        public Guid SkillId { get; set; }
        public string? SkillName { get; set; }
        public int? ProficiencyLevel { get; set; }
    }

    public class NannyAvailabilityItemViewModel
    {
        public int DayOfWeek { get; set; }
        public bool IsAvailable { get; set; }
        public int TimeSlot { get; set; }
    }

    public class NannyCertificateItemViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IssuingOrganization { get; set; }
        public string? CertificateUrl { get; set; }
        public int VerificationStatus { get; set; }
    }
}
