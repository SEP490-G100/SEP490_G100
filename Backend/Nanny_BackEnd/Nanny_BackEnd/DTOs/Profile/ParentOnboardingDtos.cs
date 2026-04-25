using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Profile;

public class UpdateParentProfileRequest
{
    [Required(ErrorMessage = "Mô tả gia đình là bắt buộc.")]
    [StringLength(1000, ErrorMessage = "Mô tả gia đình không được vượt quá 1000 ký tự.")]
    public string? FamilyDescription { get; set; }

    [Required(ErrorMessage = "Số lượng trẻ là bắt buộc.")]
    [Range(1, 20, ErrorMessage = "Số lượng trẻ phải trong khoảng 1-20.")]
    public int? NumberOfChildren { get; set; }
}

public class ParentOnboardingChildRequest
{
    [StringLength(1000, ErrorMessage = "Nhu cầu đặc biệt không được vượt quá 1000 ký tự.")]
    public string? SpecialNeeds { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    public string? Notes { get; set; }

    [StringLength(1000, ErrorMessage = "Đặc điểm không được vượt quá 1000 ký tự.")]
    public string? Characteristic { get; set; }

    [Required(ErrorMessage = "Nhóm tuổi của trẻ là bắt buộc.")]
    [Range(0, 3, ErrorMessage = "Nhóm tuổi của trẻ không hợp lệ.")]
    public byte? ChildAgeGroup { get; set; }
}
