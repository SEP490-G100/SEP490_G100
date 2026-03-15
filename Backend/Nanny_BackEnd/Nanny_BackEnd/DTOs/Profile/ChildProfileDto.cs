namespace Nanny_BackEnd.DTOs.Profile;

public class ChildProfileDto
{
    public Guid Id { get; set; }
    public Guid ParentProfileId { get; set; }
    public string? Name { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int? Gender { get; set; }
    public string? SpecialNeeds { get; set; }
    public string? Allergies { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
