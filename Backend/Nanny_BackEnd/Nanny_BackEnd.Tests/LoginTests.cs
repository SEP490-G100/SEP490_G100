using Microsoft.Extensions.Configuration;
using Moq;
using FluentAssertions;
using Nanny_BackEnd.DTOs.Auth;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Validations;

namespace Nanny_BackEnd.Tests;

public class LoginTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public LoginTests()
    {
        // Config tối thiểu để EmailService khởi tạo không lỗi
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

    // ── Helper tạo user mẫu ───────────────────────────────────────────────
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

    // ── TC1: Email không tồn tại ──────────────────────────────────────────
    [Fact]
    public async Task EmailNotFound()
    {
        _mockUserRepo.Setup(r => r.FindByEmailAsync("notfound@mail.com"))
                     .ReturnsAsync((User?)null);

        var act = () => _sut.LoginAsync(new LoginRequest
        {
            Email    = "notfound@mail.com",
            Password = "anything"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Email hoặc mật khẩu không đúng.");
    }

    // ── TC2: Tài khoản đăng ký bằng Google đăng nhập bằng mật khẩu ──────
    [Fact]
    public async Task GoogleAccount()
    {
        var user = MakeUser(authProvider: (int)AuthProvider.Google);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var act = () => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Email hoặc mật khẩu không đúng.");
    }

    // ── TC3: Sai mật khẩu ────────────────────────────────────────────────
    [Fact]
    public async Task WrongPassword()
    {
        var user = MakeUser(password: "Correct@123");
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var act = () => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Wrong@999"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Email hoặc mật khẩu không đúng.");
    }

    // ── TC4: Tài khoản Pending (chưa xác minh email) ─────────────────────
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

        isPending.Should().BeTrue();
        response.Should().BeNull();
        _mockOtp.Verify(
            o => o.GenerateAsync(user.Email, OtpPurpose.VerifyEmail, user.Id),
            Times.Once);
    }

    // ── TC5: Tài khoản bị khóa (Banned) ─────────────────────────────────
    [Fact]
    public async Task BannedUser()
    {
        var user = MakeUser(status: (int)UserStatus.Banned);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var act = () => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Tài khoản đã bị khóa. Vui lòng liên hệ hỗ trợ.");
    }

    // ── TC6: Tài khoản bị vô hiệu hóa (Inactive) ────────────────────────
    [Fact]
    public async Task InactiveUser()
    {
        var user = MakeUser(status: (int)UserStatus.Inactive);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(user.Email))
                     .ReturnsAsync(user);

        var act = () => _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "Password@123"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Tài khoản đã bị vô hiệu hóa.");
    }

    // ── TC7: Đăng nhập thành công ────────────────────────────────────────
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

        isPending.Should().BeFalse();
        response.Should().NotBeNull();
        response!.AccessToken.Should().Be("fake-access-token");
        response.RefreshToken.Should().Be("fake-refresh-token");
        response.User.Email.Should().Be(user.Email);
        response.User.Roles.Should().Contain("Parent");
    }
}
