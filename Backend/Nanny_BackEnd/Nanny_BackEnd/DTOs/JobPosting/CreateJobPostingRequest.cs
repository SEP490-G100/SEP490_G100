using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

/// <summary>
/// Request body khi tạo mới Job Posting.
/// Tất cả [Required] phải được cung cấp, các field khác là tùy chọn.
/// </summary>
public class CreateJobPostingRequest
{
    // ── THÔNG TIN CƠ BẢN (bắt buộc) ─────────────────────────────────

    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 200 ký tự.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả không được để trống.")]
    [StringLength(3000, MinimumLength = 20, ErrorMessage = "Mô tả phải từ 20 đến 3000 ký tự.")]
    public string Description { get; set; } = "";

    /// <summary>Loại công việc: 1=Toàn thời gian, 2=Bán thời gian, 3=Qua đêm</summary>
    [Required(ErrorMessage = "Loại công việc không được để trống.")]
    [Range(1, 3, ErrorMessage = "JobType phải là 1 (FullTime), 2 (PartTime), hoặc 3 (Overnight).")]
    public int JobType { get; set; }

    /// <summary>Loại lương: 1=Theo giờ, 2=Theo ngày, 3=Theo tháng</summary>
    [Required(ErrorMessage = "Loại lương không được để trống.")]
    [Range(1, 3, ErrorMessage = "SalaryType phải là 1 (Hourly), 2 (Daily), hoặc 3 (Monthly).")]
    public int SalaryType { get; set; }

    // ── LƯƠNG (ít nhất phải có SalaryMin hoặc SalaryNegotiable=true) ─

    [Range(0, 100_000_000, ErrorMessage = "Lương tối thiểu phải từ 0 đến 100,000,000.")]
    public decimal? SalaryMin { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Lương tối đa phải từ 0 đến 100,000,000.")]
    public decimal? SalaryMax { get; set; }

    public bool SalaryNegotiable { get; set; }

    // ── THỜI GIAN LÀM VIỆC ───────────────────────────────────────────

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? WorkingHoursStart { get; set; }
    public TimeOnly? WorkingHoursEnd { get; set; }

    [StringLength(100, ErrorMessage = "Ngày làm việc tối đa 100 ký tự.")]
    public string? WorkingDays { get; set; }  // VD: "Mon,Tue,Wed,Thu,Fri"

    // ── TRẺ EM ───────────────────────────────────────────────────────

    [Range(1, 10, ErrorMessage = "Số trẻ phải từ 1 đến 10.")]
    public int? NumberOfChildren { get; set; }

    // ── ĐỊA CHỈ ──────────────────────────────────────────────────────

    [StringLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
    public string? Location { get; set; }

    [StringLength(100, ErrorMessage = "Thành phố tối đa 100 ký tự.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "Quận/Huyện tối đa 100 ký tự.")]
    public string? District { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude phải từ -90 đến 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude phải từ -180 đến 180.")]
    public decimal? Longitude { get; set; }

    // ── KHÁC ─────────────────────────────────────────────────────────

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Danh sách kỹ năng yêu cầu (Guid của Skill)</summary>
    public List<Guid> RequiredSkillIds { get; set; } = [];
}
