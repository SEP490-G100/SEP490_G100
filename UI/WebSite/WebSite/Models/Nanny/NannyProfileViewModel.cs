namespace WebSite.Models.Nanny;

using WebSite.Enums;

public class NannyProfileViewModel
{
    public string? Bio { get; set; }
    public int? YearsOfExperience { get; set; }
    public EducationLevel? EducationLevel { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public int? MaxTravelDistance { get; set; }
}

