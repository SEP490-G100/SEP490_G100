namespace Nanny_BackEnd.DTOs.Profile;

using System.ComponentModel.DataAnnotations;

public class UpdatePersonalInfoRequest
{
    [Display(Name = "Họ")]
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;
    [Display(Name = "Tên")]
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = null!;
    [Display(Name = "Số điện thoại")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
    [Display(Name = "Đường dẫn ảnh đại diện")]
    [StringLength(500)]
    public string? AvatarUrl { get; set; }
    [Display(Name = "Ngày sinh")]
    public DateOnly? DateOfBirth { get; set; }
    [Display(Name = "Giới tính")]
    public int? Gender { get; set; }
    [Display(Name = "Địa chỉ")]
    [StringLength(500)]
    public string? Address { get; set; }
    [Display(Name = "Tỉnh / Thành phố")]
    [StringLength(100)]
    public string? City { get; set; }
    [Display(Name = "Quận / Huyện / Phường")]
    [StringLength(100)]
    public string? District { get; set; }
    [Display(Name = "Phường / Xã")]
    [StringLength(100)]
    public string? Ward { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Nanny-specific fields (optional, only applied for nanny role)
    [Display(Name = "Giới thiệu bản thân")]
    [StringLength(2000)]
    public string? Bio { get; set; }
    [Display(Name = "Số năm kinh nghiệm")]
    [Range(0, 80)]
    public int? YearsOfExperience { get; set; }
    [Display(Name = "Trình độ học vấn")]
    [Range(0, 10)]
    public int? EducationLevel { get; set; }
    [Display(Name = "Mức lương mong muốn tối thiểu")]
    public decimal? ExpectedSalaryMin { get; set; }
    [Display(Name = "Mức lương mong muốn tối đa")]
    public decimal? ExpectedSalaryMax { get; set; }
    [Display(Name = "Khoảng cách di chuyển tối đa")]
    [Range(0, 1000)]
    public int? MaxTravelDistance { get; set; }
    public List<Guid>? SkillIds { get; set; }
}
