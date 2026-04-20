using System.ComponentModel.DataAnnotations;

namespace WebSite.Enums;

public enum ChildAgeGroup
{
    [Display(Name = "Trẻ sơ sinh (0-1 tuổi)")]
    Baby = 0,           
    
    [Display(Name = "Trẻ mới biết đi (1-3 tuổi)")]
    Toddler = 1,        
    
    [Display(Name = "Trẻ mầm non (3-5 tuổi)")]
    Preschooler = 2,    
    
    [Display(Name = "Trẻ tiểu học (6-12 tuổi)")]
    Gradeschooler = 3   
}
