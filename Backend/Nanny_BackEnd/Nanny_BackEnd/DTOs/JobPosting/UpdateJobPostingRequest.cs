using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

/// <summary>
/// Request body khi cập nhật Job Posting.
/// Validation giống CreateJobPostingRequest.
/// Chỉ được cập nhật khi tin ở trạng thái Draft (chưa publish).
/// </summary>
public class UpdateJobPostingRequest
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 200 ký tự.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả không được để trống.")]
    [StringLength(3000, MinimumLength = 20, ErrorMessage = "Mô tả phải từ 20 đến 3000 ký tự.")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Loại công việc không được để trống.")]
    [Range(1, 3, ErrorMessage = "JobType phải là 1, 2, hoặc 3.")]
    public int JobType { get; set; }

    [Required(ErrorMessage = "Loại lương không được để trống.")]
    [Range(1, 3, ErrorMessage = "SalaryType phải là 1, 2, hoặc 3.")]
    public int SalaryType { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Lương tối thiểu phải từ 0 đến 100,000,000.")]
    public decimal? SalaryMin { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Lương tối đa phải từ 0 đến 100,000,000.")]
    public decimal? SalaryMax { get; set; }

    public bool SalaryNegotiable { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? WorkingHoursStart { get; set; }
    public TimeOnly? WorkingHoursEnd { get; set; }

    [StringLength(100)]
    public string? WorkingDays { get; set; }

    [Range(1, 10, ErrorMessage = "Số trẻ phải từ 1 đến 10.")]
    public int? NumberOfChildren { get; set; }

    [StringLength(300)]
    public string? Location { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public List<Guid> RequiredSkillIds { get; set; } = [];
}
