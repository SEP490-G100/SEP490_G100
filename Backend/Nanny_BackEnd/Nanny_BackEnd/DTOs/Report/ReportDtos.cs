using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Report;

public class CreateReportRequest
{
    [Required(ErrorMessage = "Ly do bao cao la bat buoc.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Ly do bao cao phai tu 5 den 500 ky tu.")]
    public string Reason { get; set; } = null!;

    [StringLength(2000, ErrorMessage = "Noi dung bang chung khong duoc vuot qua 2000 ky tu.")]
    public string? Evidence { get; set; }
}

// Backward-compatible DTO names (same shape)
public class CreateJobPostingReportRequest : CreateReportRequest;
public class CreateMessageReportRequest : CreateReportRequest;
