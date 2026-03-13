using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Auth;

public class GoogleLoginRequest
{
    [Required(ErrorMessage = "IdToken là bắt buộc.")]
    public string IdToken { get; set; } = null!;
}
