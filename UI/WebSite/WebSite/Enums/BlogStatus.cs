using System.ComponentModel.DataAnnotations;

namespace WebSite.Enums;

public enum BlogStatus
{
    [Display(Name = "Bản nháp")]
    Draft = 0,
    
    [Display(Name = "Đã xuất bản")]
    Published = 1,
    
    [Display(Name = "Đã lưu trữ")]
    Archived = 2
}
