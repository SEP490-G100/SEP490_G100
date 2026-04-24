using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Auth;

public class RefreshTokenRequest
{
    [Display(Name = "Mã truy cập")]
    [Required]
    public string AccessToken { get; set; } = null!;

    [Display(Name = "Mã làm mới")]
    [Required]
    public string RefreshToken { get; set; } = null!;
}
