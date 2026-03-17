namespace Nanny_BackEnd.DTOs.Profile;

public class UpdateParentProfileRequest
{
    public string? FamilyDescription { get; set; }
    public int? NumberOfChildren { get; set; }
}

public class ParentOnboardingChildRequest
{
    public string? SpecialNeeds { get; set; }
    public string? Notes { get; set; }
    public string Characteristic { get; internal set; }
    public byte? ChildAgeGroup { get; internal set; }
}
