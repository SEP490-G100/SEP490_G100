using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

/// <summary>
/// Request body khi tạo mới Job Posting.
/// Lat/Lng được hệ thống tự động geocode từ location/city/district.
/// </summary>
public class CreateJobPostingRequest
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 200 ký tự.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả không được để trống.")]
    [StringLength(3000, MinimumLength = 10, ErrorMessage = "Mô tả phải từ 10 đến 3000 ký tự.")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Loại công việc không được để trống.")]
    [Range(1, 3, ErrorMessage = "JobType phải là 1 (FullTime), 2 (PartTime), hoặc 3 (Overnight).")]
    public int JobType { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Lương tối thiểu phải từ 0 đến 100,000,000.")]
    public decimal? SalaryMin { get; set; }

    public bool SalaryNegotiable { get; set; }

    [Range(1, 10, ErrorMessage = "Số trẻ phải từ 1 đến 10.")]
    public int? NumberOfChildren { get; set; }

    [StringLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
    public string? Location { get; set; }

    [StringLength(100, ErrorMessage = "Thành phố tối đa 100 ký tự.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "Quận/Huyện tối đa 100 ký tự.")]
    public string? District { get; set; }

    [Range(0, 1, ErrorMessage = "Status phải là 0 (Draft) hoặc 1 (Active).")]
    public int Status { get; set; } = 1;
   
}

