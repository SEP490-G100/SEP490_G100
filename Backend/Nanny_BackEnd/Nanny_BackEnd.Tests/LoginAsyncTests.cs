using Microsoft.Extensions.Configuration;
using Moq;
using Nanny_BackEnd.DTOs.Auth;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Validations;

namespace Nanny_BackEnd.Tests;

public class LoginAsyncTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public LoginAsyncTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:FromName"]     = "NannyMatch",
                ["EmailSettings:FromEmail"]    = "noreply@test.com",
                ["EmailSettings:SmtpHost"]     = "localhost",
                ["EmailSettings:SmtpPort"]     = "587",
                ["EmailSettings:SmtpUser"]     = "user",
                ["EmailSettings:SmtpPassword"] = "pass",
            })
            .Build();

        var mockOtpRepo = new Mock<IOtpRepository>();

        _mockUserRepo  = new Mock<IUserRepository>();
        _mockTokenRepo = new Mock<IRefreshTokenRepository>();
        _mockJwt       = new Mock<JwtService>(config);
        _mockOtp       = new Mock<OtpService>(mockOtpRepo.Object);
        _mockEmail     = new Mock<EmailService>(config);

        var pwdValidator = new PasswordValidator(new ConfigurationBuilder().Build());

        _sut = new AuthService(
            _mockUserRepo.Object,
            _mockTokenRepo.Object,
            _mockJwt.Object,
            _mockOtp.Object,
            _mockEmail.Object,
            pwdValidator,
            config
        );
    }

    // -- Helper tạo user mẫu -----------------------------------------------
    private static User MakeUser(
        string email      = "test@mail.com",
        string password   = "Password@123",
        int    status     = (int)UserStatus.Active,
        int    authProvider = (int)AuthProvider.Email) => new()
    {
        Id           = Guid.NewGuid(),
        Email        = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Status       = status,
        AuthProvider = authProvider,
        FirstName    = "Test",
        LastName     = "User"
    };

    [Fact]
    public async Task EmailNotFound()
    {
        _mockUserRepo.Setup(r => r.FindByEmailAsync("notfound@mail.com"))
                     .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LoginAsync(new LoginRequest
        {
            Email    = "notfound@mail.com",
            Password = "anything"
        }));
    }

    [Fact]
    public async Task GoogleAccount()
    {
        var user = MakeUser(authProvider: (int)AuthProvider.Google);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        }));
    }

    // -- TC3: Sai mật khẩu ------------------------------------------------
    [Fact]
    public async Task WrongPassword()
    {
        var user = MakeUser(password: "Correct@123");
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Wrong@999"
        }));
    }

    [Fact]
    public async Task PendingUser()
    {
        var user = MakeUser(status: (int)UserStatus.Pending);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);
        _mockOtp.Setup(o => o.GenerateAsync(user.Email, OtpPurpose.VerifyEmail, user.Id))
                .ReturnsAsync("123456");
        _mockEmail.Setup(e => e.SendOtpEmailAsync(user.Email, "123456", "VerifyEmail"))
                  .Returns(Task.CompletedTask);

        var (response, isPending) = await _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        });

        Assert.True(isPending);
        Assert.Null(response);
        _mockOtp.Verify(
            o => o.GenerateAsync(user.Email, OtpPurpose.VerifyEmail, user.Id),
            Times.Once);
    }

    [Fact]
    public async Task BannedUser()
    {
        var user = MakeUser(status: (int)UserStatus.Banned);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        }));
    }

    [Fact]
    public async Task InactiveUser()
    {
        var user = MakeUser(status: (int)UserStatus.Inactive);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        }));
    }

    [Fact]
    public async Task Success()
    {
        var user = MakeUser(status: (int)UserStatus.Active);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _mockUserRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockUserRepo.Setup(r => r.GetRolesAsync(user.Id)).ReturnsAsync(["Parent"]);

        _mockTokenRepo.Setup(t => t.RevokeAllForUserAsync(user.Id)).Returns(Task.CompletedTask);
        _mockTokenRepo.Setup(t => t.Add(It.IsAny<RefreshToken>()));
        _mockTokenRepo.Setup(t => t.SaveChangesAsync()).Returns(Task.CompletedTask);

        _mockJwt.Setup(j => j.GenerateAccessToken(user, It.IsAny<List<string>>()))
                .Returns(("fake-access-token", DateTime.UtcNow.AddMinutes(30), "fake-jwt-id"));
        _mockJwt.Setup(j => j.GenerateRefreshToken())
                .Returns("fake-refresh-token");

        var (response, isPending) = await _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        });

        Assert.False(isPending);
        Assert.NotNull(response);
        Assert.Equal("fake-access-token", response!.AccessToken);
        Assert.Equal("fake-refresh-token", response.RefreshToken);
        Assert.Equal(user.Email, response.User.Email);
        Assert.Contains("Parent", response.User.Roles);
    }
}
