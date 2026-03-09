namespace Nanny_BackEnd.DTOs.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}
