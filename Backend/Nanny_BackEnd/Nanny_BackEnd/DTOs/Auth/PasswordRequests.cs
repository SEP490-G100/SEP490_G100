using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Auth;

internal static class AuthRequestValidationPatterns
{
    public const string StrictEmail = @"^(?!.*\.\.)(?!\.)(?!.*\.$)[A-Za-z0-9._%+\-']+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc.")]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
    public string NewPassword { get; set; } = null!;
}

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [RegularExpression(AuthRequestValidationPatterns.StrictEmail, ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [RegularExpression(AuthRequestValidationPatterns.StrictEmail, ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP phải có 6 ký tự.")]
    public string OtpCode { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(8)]
    public string NewPassword { get; set; } = null!;
}

public class VerifyEmailRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [RegularExpression(AuthRequestValidationPatterns.StrictEmail, ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
    [StringLength(6, MinimumLength = 6)]
    public string OtpCode { get; set; } = null!;
}
