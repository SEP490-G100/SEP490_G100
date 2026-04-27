using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Auth;

public class GoogleLoginRequest
{
    [Required(ErrorMessage = "Mã xác thực Google là bắt buộc.")]
    public string IdToken { get; set; } = null!;
}
