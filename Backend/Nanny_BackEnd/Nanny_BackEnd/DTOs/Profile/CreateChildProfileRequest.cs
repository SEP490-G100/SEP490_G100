namespace Nanny_BackEnd.DTOs.Profile;

using System.ComponentModel.DataAnnotations;
using Nanny_BackEnd.Enums;

public class CreateChildProfileRequest
{
    [Display(Name = "Nhu cầu đặc biệt")]
    [StringLength(1000)]
    public string? SpecialNeeds { get; set; }
    [Display(Name = "Ghi chú")]
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Display(Name = "Đặc điểm")]
    [StringLength(1000)]
    public string? Characteristic { get; set; }
    [Display(Name = "Nhóm tuổi của trẻ")]
    [Required]
    [EnumDataType(typeof(ChildAgeGroup))]
    public ChildAgeGroup? ChildAgeGroup { get; set; }
}
