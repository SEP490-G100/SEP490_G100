using System.ComponentModel.DataAnnotations;
using WebSite.Validation;

namespace WebSite.Models.Nanny;

using WebSite.Enums;

public class NannyProfileViewModel : IValidatableObject
{
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public EducationLevel? EducationLevel { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public int? MaxTravelDistance { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        SalaryValidationRules.Validate(
            ExpectedSalaryMin,
            ExpectedSalaryMax,
            nameof(ExpectedSalaryMin),
            nameof(ExpectedSalaryMax),
            "Mức lương tối thiểu",
            "Mức lương tối đa");
}
