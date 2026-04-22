using Microsoft.Extensions.Configuration;
using Moq;
using Nanny_BackEnd.DTOs.Auth;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;
using Nanny_BackEnd.Validations;

namespace Nanny_BackEnd.Tests;

public class RefreshTokenTests
{
    private readonly Mock<IUserRepository>         _userRepo;
    private readonly Mock<IRefreshTokenRepository> _tokenRepo;
    private readonly Mock<IJwtService>            _jwt;
    private readonly AuthService                  _sut;

    public RefreshTokenTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:FromName"]  = "N",
                ["EmailSettings:FromEmail"] = "n@t.c",
            })
            .Build();

        _userRepo  = new Mock<IUserRepository>();
        _tokenRepo = new Mock<IRefreshTokenRepository>();
        _jwt       = new Mock<IJwtService>();
        var mockOtp  = new Mock<IOtpService>();
        var mockEmail = new Mock<IEmailService>();

        _sut = new AuthService(
            _userRepo.Object,
            _tokenRepo.Object,
            _jwt.Object,
            mockOtp.Object,
            mockEmail.Object,
            new PasswordValidator(new ConfigurationBuilder().Build()),
            config);
    }

    [Fact]
    public async Task InvalidAccessTokenClaims_Throws()
    {
        _jwt.Setup(j => j.GetTokenClaims(It.IsAny<string>()))
            .Returns((null, null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RefreshTokenAsync(new RefreshTokenRequest
        {
            AccessToken  = "bad",
            RefreshToken = "ref"
        }));
    }

    [Fact]
    public async Task RefreshTokenNotInStore_Throws()
    {
        var uid = Guid.NewGuid();
        _jwt.Setup(j => j.GetTokenClaims(It.IsAny<string>())).Returns((uid, "jid"));
        _tokenRepo.Setup(t => t.FindByTokenAsync("missing")).ReturnsAsync((RefreshToken?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.RefreshTokenAsync(new RefreshTokenRequest
        {
            AccessToken  = "a",
            RefreshToken = "missing"
        }));
    }

    [Fact]
    public async Task Success_ReturnsNewLoginResponse_MarksTokenUsed()
    {
        var userId  = Guid.NewGuid();
        var jwtId   = "j1";
        var storedT = "rt-old";
        var stored = new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            Token     = storedT,
            JwtId     = jwtId,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false,
            IsUsed    = false,
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            Id     = userId,
            Email  = "u@t.c",
            Status = (int)UserStatus.Active,
            FirstName = "A", LastName = "B"
        };

        _jwt.Setup(j => j.GetTokenClaims("at")).Returns((userId, jwtId));
        _tokenRepo.Setup(t => t.FindByTokenAsync(storedT)).ReturnsAsync(stored);
        _userRepo.Setup(r => r.FindByIdAsync(userId)).ReturnsAsync(user);
        _userRepo.Setup(r => r.GetRolesAsync(userId)).ReturnsAsync(new List<string> { "Nanny" });
        _jwt.Setup(j => j.GenerateAccessToken(user, It.IsAny<List<string>>()))
            .Returns(("new-access", DateTime.UtcNow.AddHours(1), "j2"));
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh");
        _tokenRepo.Setup(t => t.SaveChangesAsync()).Returns(Task.CompletedTask);

        var response = await _sut.RefreshTokenAsync(new RefreshTokenRequest
        {
            AccessToken  = "at",
            RefreshToken = storedT
        });

        Assert.Equal("new-access", response.AccessToken);
        Assert.Equal("new-refresh", response.RefreshToken);
        Assert.True(stored.IsUsed);
        /* BuildLoginResponse + cuối RefreshTokenAsync mỗi nơi gọi SaveChangesAsync */
        _tokenRepo.Verify(t => t.SaveChangesAsync(), Times.Exactly(2));
    }
}
