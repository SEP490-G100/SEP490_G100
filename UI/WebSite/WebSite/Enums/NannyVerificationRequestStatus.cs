using System.ComponentModel.DataAnnotations;

namespace WebSite.Enums;

public enum NannyVerificationRequestStatus
{
    [Display(Name = "Đang chờ duyệt")]
    Pending = 1,

    [Display(Name = "Đã duyệt")]
    Approved = 2,

    [Display(Name = "Từ chối")]
    Rejected = 3
}
