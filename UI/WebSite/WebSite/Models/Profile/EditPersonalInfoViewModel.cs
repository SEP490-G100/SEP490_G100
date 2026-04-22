using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using WebSite.Models.Nanny;
using WebSite.Validation;

namespace WebSite.Models.Profile
{
    public class EditPersonalInfoViewModel : IValidatableObject
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        public IFormFile? AvatarFile { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public int? Gender { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Nanny specific
        public List<string> Roles { get; set; } = new();
        public bool IsNanny => Roles.Any(r => r.Equals("Nanny", StringComparison.OrdinalIgnoreCase));
        public string? Bio { get; set; }
        public int? YearsOfExperience { get; set; }
        public int? EducationLevel { get; set; }
        public decimal? ExpectedSalaryMin { get; set; }
        public decimal? ExpectedSalaryMax { get; set; }
        public int? MaxTravelDistance { get; set; }
        public List<Guid> SelectedSkillIds { get; set; } = new();
        public List<SelectableSkillViewModel> AvailableSkills { get; set; } = new();
        public List<DayAvailabilityViewModel> Availability { get; set; } = new()
        {
            new() { DayOfWeek = 1, DayName = "Thứ 2" },
            new() { DayOfWeek = 2, DayName = "Thứ 3" },
            new() { DayOfWeek = 3, DayName = "Thứ 4" },
            new() { DayOfWeek = 4, DayName = "Thứ 5" },
            new() { DayOfWeek = 5, DayName = "Thứ 6" },
            new() { DayOfWeek = 6, DayName = "Thứ 7" },
            new() { DayOfWeek = 0, DayName = "Chủ nhật" }
        };
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!IsNanny)
                yield break;

            foreach (var error in SalaryValidationRules.Validate(
                         ExpectedSalaryMin,
                         ExpectedSalaryMax,
                         nameof(ExpectedSalaryMin),
                         nameof(ExpectedSalaryMax)))
            {
                yield return error;
            }
        }
    }

    public class SelectableSkillViewModel
    {
        public Guid SkillId { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
}

