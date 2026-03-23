namespace Nanny_BackEnd.DTOs.JobPosting;

public class JobPostingPrefillResponse
{
    public int? NumberOfChildren { get; set; }
    public Guid? SelectedChildProfileId { get; set; }
    public string? Characteristic { get; set; }
    public int? BirthType { get; set; }
    public string? BirthTypeLabel { get; set; }
    public string? SpecialNeeds { get; set; }
    public List<string> Skills { get; set; } = [];
    public List<JobPostingPrefillChildResponse> Children { get; set; } = [];
}

public class JobPostingPrefillChildResponse
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
    public string? Characteristic { get; set; }
    public int? BirthType { get; set; }
    public string? BirthTypeLabel { get; set; }
    public string? SpecialNeeds { get; set; }
}
