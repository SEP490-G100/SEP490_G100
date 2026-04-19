using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Auth;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email lÃ  báº¯t buá»™c.")]
    [EmailAddress(ErrorMessage = "Email khÃ´ng há»£p lá»‡.")]
    public string Email { get; set; } = null!;
}

public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "Email lÃ  báº¯t buá»™c.")]
    [EmailAddress(ErrorMessage = "Email khÃ´ng há»£p lá»‡.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "MÃ£ OTP lÃ  báº¯t buá»™c.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP pháº£i cÃ³ 6 kÃ½ tá»±.")]
    public string OtpCode { get; set; } = null!;

    [Required(ErrorMessage = "Máº­t kháº©u má»›i lÃ  báº¯t buá»™c.")]
    [MinLength(8, ErrorMessage = "Máº­t kháº©u pháº£i cÃ³ Ã­t nháº¥t 8 kÃ½ tá»±.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "XÃ¡c nháº­n máº­t kháº©u lÃ  báº¯t buá»™c.")]
    [Compare("NewPassword", ErrorMessage = "Máº­t kháº©u xÃ¡c nháº­n khÃ´ng khá»›p.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = null!;
}

public class VerifyEmailViewModel
{
    [Required(ErrorMessage = "Email lÃ  báº¯t buá»™c.")]
    [EmailAddress(ErrorMessage = "Email khÃ´ng há»£p lá»‡.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "MÃ£ OTP lÃ  báº¯t buá»™c.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP pháº£i cÃ³ 6 kÃ½ tá»±.")]
    public string OtpCode { get; set; } = null!;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Máº­t kháº©u hiá»‡n táº¡i lÃ  báº¯t buá»™c.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "Máº­t kháº©u má»›i lÃ  báº¯t buá»™c.")]
    [MinLength(8, ErrorMessage = "Máº­t kháº©u pháº£i cÃ³ Ã­t nháº¥t 8 kÃ½ tá»±.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "XÃ¡c nháº­n máº­t kháº©u lÃ  báº¯t buá»™c.")]
    [Compare("NewPassword", ErrorMessage = "Máº­t kháº©u xÃ¡c nháº­n khÃ´ng khá»›p.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = null!;
}

