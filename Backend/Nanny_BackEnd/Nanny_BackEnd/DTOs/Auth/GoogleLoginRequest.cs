using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Auth;

public class GoogleLoginRequest
{
    [Required(ErrorMessage = "IdToken là bắt buộc.")]
    public string IdToken { get; set; } = null!;
}

public class GoogleAuthCodeRequest
{
    [Required]
    public string AuthCode { get; set; } = null!;

    [Required]
    public string RedirectUri { get; set; } = null!;
}
