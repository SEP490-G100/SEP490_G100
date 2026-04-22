using Microsoft.Extensions.Configuration;
using Moq;
using FluentAssertions;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Validations;

namespace Nanny_BackEnd.Tests;

public class ForgotPasswordTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public ForgotPasswordTests()
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

        _sut = new AuthService(
            _mockUserRepo.Object,
            _mockTokenRepo.Object,
            _mockJwt.Object,
            _mockOtp.Object,
            _mockEmail.Object,
            new PasswordValidator(new ConfigurationBuilder().Build()),
            config
        );
    }

    // ── TC1: Email không tồn tại ──────────────────────────────────────────
    // Trả về message chung để tránh lộ thông tin email có tồn tại hay không
    [Fact]
    public async Task EmailNotFound()
    {
        _mockUserRepo.Setup(r => r.FindByEmailAsync("notfound@mail.com"))
                     .ReturnsAsync((User?)null);

        var (success, message) = await _sut.ForgotPasswordAsync("notfound@mail.com");

        success.Should().BeTrue();
        message.Should().Be("Nếu email tồn tại, mã OTP đã được gửi.");

        // Không gửi OTP khi email không tồn tại
        _mockOtp.Verify(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<Guid?>()), Times.Never);
    }

    // ── TC2: Tài khoản đăng ký bằng Google ───────────────────────────────
    // Không gửi OTP vì Google account không dùng mật khẩu
    [Fact]
    public async Task GoogleAccount()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "google@mail.com",
            AuthProvider = (int)AuthProvider.Google,
            Status       = (int)UserStatus.Active
        };

        _mockUserRepo.Setup(r => r.FindByEmailAsync("google@mail.com"))
                     .ReturnsAsync(user);

        var (success, message) = await _sut.ForgotPasswordAsync("google@mail.com");

        success.Should().BeTrue();
        message.Should().Be("Nếu email tồn tại, mã OTP đã được gửi.");

        _mockOtp.Verify(o => o.GenerateAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<Guid?>()), Times.Never);
    }

    // ── TC3: Gửi OTP thành công ───────────────────────────────────────────
    [Fact]
    public async Task Success()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "user@mail.com",
            AuthProvider = (int)AuthProvider.Email,
            Status       = (int)UserStatus.Active
        };

        _mockUserRepo.Setup(r => r.FindByEmailAsync("user@mail.com"))
                     .ReturnsAsync(user);
        _mockOtp.Setup(o => o.GenerateAsync("user@mail.com", OtpPurpose.ForgotPassword, user.Id))
                .ReturnsAsync("888999");
        _mockEmail.Setup(e => e.SendOtpEmailAsync("user@mail.com", "888999", "ForgotPassword"))
                  .Returns(Task.CompletedTask);

        var (success, message) = await _sut.ForgotPasswordAsync("user@mail.com");

        success.Should().BeTrue();
        message.Should().Be("Mã OTP đã được gửi đến email của bạn.");

        _mockOtp.Verify(o => o.GenerateAsync("user@mail.com", OtpPurpose.ForgotPassword, user.Id), Times.Once);
    }

    // ── TC4: Gửi email thất bại (SMTP lỗi, mạng down...) ─────────────────
    [Fact]
    public async Task EmailFails()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "user@mail.com",
            AuthProvider = (int)AuthProvider.Email,
            Status       = (int)UserStatus.Active
        };

        _mockUserRepo.Setup(r => r.FindByEmailAsync("user@mail.com"))
                     .ReturnsAsync(user);
        _mockOtp.Setup(o => o.GenerateAsync("user@mail.com", OtpPurpose.ForgotPassword, user.Id))
                .ReturnsAsync("888999");
        _mockEmail.Setup(e => e.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .ThrowsAsync(new Exception("SMTP connection failed"));

        var (success, message) = await _sut.ForgotPasswordAsync("user@mail.com");

        success.Should().BeFalse();
        message.Should().Be("Không thể gửi email lúc này. Vui lòng thử lại sau.");
    }
}
