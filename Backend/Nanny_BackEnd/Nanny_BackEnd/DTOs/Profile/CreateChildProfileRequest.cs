namespace Nanny_BackEnd.DTOs.Profile;

using System.ComponentModel.DataAnnotations;
using Nanny_BackEnd.Enums;

public class CreateChildProfileRequest
{
    [StringLength(1000)]
    public string? SpecialNeeds { get; set; }
    [StringLength(1000)]
    public string? Notes { get; set; }
    [StringLength(1000)]
    public string? Characteristic { get; set; }
    [Required]
    [EnumDataType(typeof(ChildAgeGroup))]
    public ChildAgeGroup? ChildAgeGroup { get; set; }
}
