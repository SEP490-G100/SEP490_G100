using System.ComponentModel.DataAnnotations;

namespace WebSite.Enums;

public enum EducationLevel
{
    [Display(Name = "Trung học phổ thông")]
    HighSchool = 0,
    
    [Display(Name = "Cao đẳng")]
    College = 1,
    
    [Display(Name = "Cử nhân")]
    Bachelor = 2,
    
    [Display(Name = "Thạc sĩ")]
    Master = 3,
    
    [Display(Name = "Khác")]
    Other = 4
}
