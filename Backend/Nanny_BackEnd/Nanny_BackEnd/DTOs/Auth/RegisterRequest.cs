using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Họ là bắt buộc.")]
    public string FirstName { get; set; } = null!;

    [Required(ErrorMessage = "Tên là bắt buộc.")]
    public string LastName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Role sẽ được set sau trong bước ChooseRole, không gán mặc định.
    /// Nếu null/empty, GetStatusAsync() sẽ yêu cầu user chọn role trước khi onboarding
    /// </summary>
    public string? Role { get; set; } = null;
}
