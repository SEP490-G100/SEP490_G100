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

public class RegisterTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public RegisterTests()
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

        // PasswordValidator dùng default config
        // → MinLength=8, RequireDigit, RequireUppercase, RequireLowercase, RequireSpecialChar
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

    // ── Helper ────────────────────────────────────────────────────────────
    private static RegisterRequest ValidRequest(string email = "new@mail.com") => new()
    {
        FirstName = "Nguyen",
        LastName  = "An",
        Email     = email,
        Password  = "Password@123"
    };

    private static User ExistingUser(
        string email      = "existing@mail.com",
        int    status     = (int)UserStatus.Active,
        int    authProvider = (int)AuthProvider.Email) => new()
    {
        Id           = Guid.NewGuid(),
        Email        = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
        Status       = status,
        AuthProvider = authProvider,
        FirstName    = "Test",
        LastName     = "User"
    };

    // ── TC1: Email đã tồn tại (Active, Email provider) ────────────────────
    [Fact]
    public async Task DuplicateEmail()
    {
        var existing = ExistingUser(status: (int)UserStatus.Active);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(existing.Email))
                     .ReturnsAsync(existing);

        var act = () => _sut.RegisterAsync(new RegisterRequest
        {
            Email    = existing.Email,
            Password = "Password@123"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Email đã được đăng ký.");
    }

    // ── TC2: Email đã đăng ký bằng Google ────────────────────────────────
    [Fact]
    public async Task GoogleEmail()
    {
        var existing = ExistingUser(authProvider: (int)AuthProvider.Google);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(existing.Email))
                     .ReturnsAsync(existing);

        var act = () => _sut.RegisterAsync(new RegisterRequest
        {
            Email    = existing.Email,
            Password = "Password@123"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Email này đã đăng ký bằng Google. Vui lòng đăng nhập bằng Google.");
    }

    // ── TC3: Email tồn tại nhưng đang Pending → gửi lại OTP, không throw ─
    [Fact]
    public async Task PendingEmail()
    {
        var existing = ExistingUser(status: (int)UserStatus.Pending);
        _mockUserRepo.Setup(r => r.FindByEmailAsync(existing.Email))
                     .ReturnsAsync(existing);
        _mockOtp.Setup(o => o.GenerateAsync(existing.Email, OtpPurpose.VerifyEmail, existing.Id))
                .ReturnsAsync("654321");
        _mockEmail.Setup(e => e.SendOtpEmailAsync(existing.Email, "654321", "VerifyEmail"))
                  .Returns(Task.CompletedTask);

        var act = () => _sut.RegisterAsync(new RegisterRequest
        {
            Email    = existing.Email,
            Password = "Password@123"
        });

        // Không throw — chỉ gửi lại OTP
        await act.Should().NotThrowAsync();
        _mockOtp.Verify(
            o => o.GenerateAsync(existing.Email, OtpPurpose.VerifyEmail, existing.Id),
            Times.Once);
    }

    // ── TC4: Mật khẩu quá ngắn ───────────────────────────────────────────
    [Fact]
    public async Task PasswordTooShort()
    {
        _mockUserRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>()))
                     .ReturnsAsync((User?)null);

        var act = () => _sut.RegisterAsync(new RegisterRequest
        {
            Email    = "new@mail.com",
            Password = "Ab@1"           // < 8 ký tự
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*ít nhất 8 ký tự*");
    }

    // ── TC5: Mật khẩu thiếu ký tự đặc biệt ──────────────────────────────
    [Fact]
    public async Task PasswordNoSpecialChar()
    {
        _mockUserRepo.Setup(r => r.FindByEmailAsync(It.IsAny<string>()))
                     .ReturnsAsync((User?)null);

        var act = () => _sut.RegisterAsync(new RegisterRequest
        {
            Email    = "new@mail.com",
            Password = "Password123"    // không có ký tự đặc biệt
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*ký tự đặc biệt*");
    }

    // ── TC6: Đăng ký thành công ───────────────────────────────────────────
    [Fact]
    public async Task Success()
    {
        _mockUserRepo.Setup(r => r.FindByEmailAsync("new@mail.com"))
                     .ReturnsAsync((User?)null);
        _mockUserRepo.Setup(r => r.SaveChangesAsync())
                     .Returns(Task.CompletedTask);
        _mockOtp.Setup(o => o.GenerateAsync("new@mail.com", OtpPurpose.VerifyEmail, It.IsAny<Guid>()))
                .ReturnsAsync("111222");
        _mockEmail.Setup(e => e.SendOtpEmailAsync("new@mail.com", "111222", "VerifyEmail"))
                  .Returns(Task.CompletedTask);

        var act = () => _sut.RegisterAsync(ValidRequest("new@mail.com"));

        // Không throw
        await act.Should().NotThrowAsync();

        // SaveChangesAsync được gọi 1 lần
        _mockUserRepo.Verify(r => r.SaveChangesAsync(), Times.Once);

        // OTP được gửi 1 lần
        _mockOtp.Verify(
            o => o.GenerateAsync("new@mail.com", OtpPurpose.VerifyEmail, It.IsAny<Guid>()),
            Times.Once);
    }
}
