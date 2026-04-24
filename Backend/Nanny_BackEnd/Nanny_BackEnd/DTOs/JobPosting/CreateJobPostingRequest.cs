using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

public class CreateJobPostingRequest
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 200 ký tự.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả không được để trống.")]
    [StringLength(3000, MinimumLength = 10, ErrorMessage = "Mô tả phải từ 10 đến 3000 ký tự.")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Loại công việc không được để trống.")]
    [Range(1, 3, ErrorMessage = "Loại công việc phải là 1 (Toàn thời gian), 2 (Bán thời gian) hoặc 3 (Qua đêm).")]
    public int JobType { get; set; }

    [Range(8_000_000, 50_000_000, ErrorMessage = "Lương tối thiểu phải trong khoảng 8.000.000 - 50.000.000 VND.")]
    public decimal? SalaryMin { get; set; }

    [Range(8_000_000, 50_000_000, ErrorMessage = "Lương tối đa phải trong khoảng 8.000.000 - 50.000.000 VND.")]
    public decimal? SalaryMax { get; set; }

    public bool SalaryNegotiable { get; set; }

    [Range(1, 10, ErrorMessage = "Số trẻ phải từ 1 đến 10.")]
    public int? NumberOfChildren { get; set; }

    public Guid? ChildProfileId { get; set; }
    public List<Guid> ChildProfileIds { get; set; } = [];

    [StringLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
    public string? Location { get; set; }

    [StringLength(100, ErrorMessage = "Tỉnh hoặc thành phố tối đa 100 ký tự.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "Quận hoặc huyện tối đa 100 ký tự.")]
    public string? District { get; set; }

    [StringLength(500, ErrorMessage = "Đặc điểm của trẻ tối đa 500 ký tự.")]
    public string? Characteristic { get; set; }

    [Range(1, 4, ErrorMessage = "Nhóm tuổi của trẻ phải từ 1 đến 4.")]
    public int? BirthType { get; set; }

    [StringLength(500, ErrorMessage = "Nhu cầu đặc biệt tối đa 500 ký tự.")]
    public string? SpecialNeeds { get; set; }

    [Range(18, 80, ErrorMessage = "Độ tuổi tối thiểu của bảo mẫu phải từ 18 đến 80.")]
    public int? MinNannyAge { get; set; }

    [Range(18, 80, ErrorMessage = "Độ tuổi tối đa của bảo mẫu phải từ 18 đến 80.")]
    public int? MaxNannyAge { get; set; }

    [MaxLength(20, ErrorMessage = "Mỗi bài đăng chỉ được chọn tối đa 20 kỹ năng.")]
    public List<string> Skills { get; set; } = [];

    public List<JobScheduleSlotRequest> ScheduleSlots { get; set; } = [];

    [Range(1, 2, ErrorMessage = "Trạng thái bài đăng phải là 1 (Đang hiển thị) hoặc 2 (Đã ẩn).")]
    public int Status { get; set; } = 1;
}
