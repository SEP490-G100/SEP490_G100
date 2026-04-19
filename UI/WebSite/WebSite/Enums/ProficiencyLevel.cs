using System.ComponentModel.DataAnnotations;

namespace WebSite.Enums;

public enum ProficiencyLevel
{
    [Display(Name = "Cơ bản")]
    Basic = 1,
    
    [Display(Name = "Trung cấp")]
    Intermediate = 2,
    
    [Display(Name = "Nâng cao")]
    Advanced = 3
}
