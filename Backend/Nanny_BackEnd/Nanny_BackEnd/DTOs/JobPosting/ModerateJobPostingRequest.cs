using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

public class ModerateJobPostingRequest
{
    [Display(Name = "Quyết định xử lý")]
    [Required(ErrorMessage = "Vui lòng chọn quyết định xử lý.")]
    public int Action { get; set; }

    [Display(Name = "Ghi chú của điều hành viên")]
    [StringLength(500, ErrorMessage = "Ghi chú của điều hành viên không được vượt quá 500 ký tự.")]
    public string? Note { get; set; }
}
