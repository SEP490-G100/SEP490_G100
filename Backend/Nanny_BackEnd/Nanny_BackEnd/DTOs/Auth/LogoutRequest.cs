namespace Nanny_BackEnd.DTOs.Auth;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = null!;
}
