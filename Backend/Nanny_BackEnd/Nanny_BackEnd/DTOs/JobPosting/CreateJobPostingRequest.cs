using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

public class CreateJobPostingRequest
{
    [Required(ErrorMessage = "Tieu de khong duoc de trong.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Tieu de phai tu 5 den 200 ky tu.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mo ta khong duoc de trong.")]
    [StringLength(3000, MinimumLength = 10, ErrorMessage = "Mo ta phai tu 10 den 3000 ky tu.")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Loai cong viec khong duoc de trong.")]
    [Range(1, 3, ErrorMessage = "JobType phai la 1 (FullTime), 2 (PartTime), hoac 3 (Overnight).")]
    public int JobType { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Luong toi thieu phai tu 0 den 100,000,000.")]
    public decimal? SalaryMin { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Luong toi da phai tu 0 den 100,000,000.")]
    public decimal? SalaryMax { get; set; }

    public bool SalaryNegotiable { get; set; }

    [Range(1, 10, ErrorMessage = "So tre phai tu 1 den 10.")]
    public int? NumberOfChildren { get; set; }

    public Guid? ChildProfileId { get; set; }

    [StringLength(300, ErrorMessage = "Dia chi toi da 300 ky tu.")]
    public string? Location { get; set; }

    [StringLength(100, ErrorMessage = "Thanh pho toi da 100 ky tu.")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "Quan/Huyen toi da 100 ky tu.")]
    public string? District { get; set; }

    [StringLength(500, ErrorMessage = "Characteristic toi da 500 ky tu.")]
    public string? Characteristic { get; set; }

    [Range(1, 4, ErrorMessage = "BirthType phai tu 1 den 4.")]
    public int? BirthType { get; set; }

    [StringLength(500, ErrorMessage = "SpecialNeeds toi da 500 ky tu.")]
    public string? SpecialNeeds { get; set; }

    [Range(18, 80, ErrorMessage = "Do tuoi toi thieu cua bao mau phai tu 18 den 80.")]
    public int? MinNannyAge { get; set; }

    [Range(18, 80, ErrorMessage = "Do tuoi toi da cua bao mau phai tu 18 den 80.")]
    public int? MaxNannyAge { get; set; }

    [MaxLength(20, ErrorMessage = "Toi da 20 ky nang cho moi bai dang.")]
    public List<string> Skills { get; set; } = [];

    public List<JobScheduleSlotRequest> ScheduleSlots { get; set; } = [];

    [Range(1, 2, ErrorMessage = "Trạng thái bài đăng phải là 1 (Published) hoặc 2 (Hidden).")]
    public int Status { get; set; } = 1;
}
