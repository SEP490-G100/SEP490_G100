using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

/// <summary>
/// Request body khi cập nhật Job Posting.
/// Lat/Lng được hệ thống tự động geocode nếu địa chỉ thay đổi.
/// </summary>
public class UpdateJobPostingRequest
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 200 ký tự.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả không được để trống.")]
    [StringLength(3000, MinimumLength = 10, ErrorMessage = "Mô tả phải từ 10 đến 3000 ký tự.")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Loại công việc không được để trống.")]
    [Range(1, 3, ErrorMessage = "JobType phải là 1, 2, hoặc 3.")]
    public int JobType { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Lương tối thiểu phải từ 0 đến 100,000,000.")]
    public decimal? SalaryMin { get; set; }

    public bool SalaryNegotiable { get; set; }

    [Range(1, 10, ErrorMessage = "Số trẻ phải từ 1 đến 10.")]
    public int? NumberOfChildren { get; set; }

    [StringLength(300)]
    public string? Location { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    /// <summary>Trạng thái: 0 = Draft (chỉ Nanny xem), 1 = Active (công khai).</summary>
    [Range(0, 1, ErrorMessage = "Status phải là 0 (Draft) hoặc 1 (Active).")]
    public int Status { get; set; } = 1;
    // Lat/Lng được hệ thống tự geocode — không cần client gửi lên
}
