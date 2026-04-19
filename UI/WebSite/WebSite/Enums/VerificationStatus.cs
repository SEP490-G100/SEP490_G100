using System.ComponentModel.DataAnnotations;

namespace WebSite.Enums;

public enum VerificationStatus
{
    [Display(Name = "Chưa nộp")]
    NotSubmitted = 0,
    
    [Display(Name = "Đang chờ duyệt")]
    Pending = 1,
    
    [Display(Name = "Đã duyệt")]
    Approved = 2,
    
    [Display(Name = "Từ chối")]
    Rejected = 3
}
