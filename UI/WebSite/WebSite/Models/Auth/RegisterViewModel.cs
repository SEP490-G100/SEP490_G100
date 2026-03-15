using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Auth;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = null!;

    [Required(ErrorMessage = "Họ là bắt buộc.")]
    public string FirstName { get; set; } = null!;

    [Required(ErrorMessage = "Tên là bắt buộc.")]
    public string LastName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    public string Role { get; set; } = "Parent"; // Parent hoặc Nanny
}
