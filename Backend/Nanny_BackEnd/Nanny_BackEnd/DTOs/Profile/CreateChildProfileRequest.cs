namespace Nanny_BackEnd.DTOs.Profile;

public class CreateChildProfileRequest
{
    public string? Characteristic { get; set; }
    public byte? ChildAgeGroup { get; set; }
    public string? SpecialNeeds { get; set; }
    public string? Notes { get; set; }
}
