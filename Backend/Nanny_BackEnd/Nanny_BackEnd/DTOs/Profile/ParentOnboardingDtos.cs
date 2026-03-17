namespace Nanny_BackEnd.DTOs.Profile;

public class UpdateParentProfileRequest
{
    public string? FamilyDescription { get; set; }
    public int? NumberOfChildren { get; set; }
}

public class ParentOnboardingChildRequest
{
    public string? Characteristic { get; set; }
    public byte? ChildAgeGroup { get; set; }
    public string? SpecialNeeds { get; set; }
    public string? Notes { get; set; }
}
