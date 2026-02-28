using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Auth;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;
}

public class ResetPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP phải có 6 ký tự.")]
    public string OtpCode { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [MinLength(8)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = null!;
}

public class VerifyEmailViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
    [StringLength(6, MinimumLength = 6)]
    public string OtpCode { get; set; } = null!;
}
