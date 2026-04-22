using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;

using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config) => _config = config;

    public virtual (string Token, DateTime ExpiresAt, string JwtId) GenerateAccessToken(User user, List<string> roles)
    {
        var jwtId = Guid.NewGuid().ToString();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_config.GetValue<int>("Jwt:AccessTokenMinutes", 30));
        var authProvider = user.AuthProvider == (int)AuthProvider.Google ? "google" : "email";
        var firstName = user.FirstName ?? string.Empty;
        var lastName = user.LastName ?? string.Empty;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new("firstName", firstName),
            new("lastName", lastName),
            new("authProvider", authProvider),
        };

        foreach (var role in roles.Where(static r => !string.IsNullOrWhiteSpace(r)))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt, jwtId);
    }

    public virtual string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Trích xuất UserId và JwtId từ token (không check lifetime — dùng cho refresh flow).
    /// </summary>
    public (Guid? UserId, string? JwtId) GetTokenClaims(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = false
            }, out _);

            var userIdStr = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var jwtId     = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            var userId = userIdStr != null ? Guid.Parse(userIdStr) : (Guid?)null;
            return (userId, jwtId);
        }
        catch
        {
            return (null, null);
        }
    }
}
