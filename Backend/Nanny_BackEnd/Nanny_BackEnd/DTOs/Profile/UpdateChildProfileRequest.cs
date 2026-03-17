namespace Nanny_BackEnd.DTOs.Profile;

using Nanny_BackEnd.Enums;

public class UpdateChildProfileRequest
{
    public string? SpecialNeeds { get; set; }
    public string? Notes { get; set; }
    public string? Characteristic { get; set; }
    public ChildAgeGroup? ChildAgeGroup { get; set; }
}
