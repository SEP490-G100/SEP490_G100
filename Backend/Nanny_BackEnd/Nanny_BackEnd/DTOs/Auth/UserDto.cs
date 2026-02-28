using Nanny_BackEnd.Helpers;

namespace Nanny_BackEnd.DTOs.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public bool EmailConfirmed { get; set; }
    public string AuthProvider { get; set; } = "email";
    public List<string> Roles { get; set; } = new();
    public string FullName => $"{FirstName} {LastName}".Trim();
}
