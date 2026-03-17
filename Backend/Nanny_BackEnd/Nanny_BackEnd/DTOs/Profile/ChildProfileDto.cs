namespace Nanny_BackEnd.DTOs.Profile;

public class ChildProfileDto
{
    public Guid Id { get; set; }
    public Guid ParentProfileId { get; set; }
    public string? Characteristic { get; set; }
    public byte? ChildAgeGroup { get; set; }
    public string? SpecialNeeds { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
