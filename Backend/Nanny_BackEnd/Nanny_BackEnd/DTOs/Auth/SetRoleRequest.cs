using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Auth;

public class SetRoleRequest
{
    [Required(ErrorMessage = "Vai trò là bắt buộc.")]
    public string Role { get; set; } = null!;
}
