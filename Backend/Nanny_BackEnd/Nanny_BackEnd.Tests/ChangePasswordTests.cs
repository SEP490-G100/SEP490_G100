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

public class ChangePasswordTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public ChangePasswordTests()
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

    // ── TC1: User không tồn tại ───────────────────────────────────────────
    [Fact]
    public async Task UserNotFound()
    {
        var userId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.FindByIdAsync(userId))
                     .ReturnsAsync((User?)null);

        var act = () => _sut.ChangePasswordAsync(userId, new ChangePasswordRequest
        {
            CurrentPassword = "Old@123",
            NewPassword     = "New@123"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Người dùng không tồn tại.");
    }

    // ── TC2: Tài khoản Google không dùng mật khẩu ────────────────────────
    [Fact]
    public async Task GoogleAccount()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            AuthProvider = (int)AuthProvider.Google,
            Status       = (int)UserStatus.Active
        };
        _mockUserRepo.Setup(r => r.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var act = () => _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "Old@123",
            NewPassword     = "New@123"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Tài khoản Google không sử dụng mật khẩu.");
    }

    // ── TC3: Mật khẩu hiện tại sai ───────────────────────────────────────
    [Fact]
    public async Task WrongCurrentPassword()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            AuthProvider = (int)AuthProvider.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct@123")
        };
        _mockUserRepo.Setup(r => r.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var act = () => _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "Wrong@999",
            NewPassword     = "New@123"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Mật khẩu hiện tại không đúng.");
    }

    // ── TC4: Mật khẩu mới không hợp lệ ──────────────────────────────────
    [Fact]
    public async Task WeakNewPassword()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            AuthProvider = (int)AuthProvider.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct@123")
        };
        _mockUserRepo.Setup(r => r.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var act = () => _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "Correct@123",
            NewPassword     = "weak"         // quá ngắn, thiếu hoa/số/ký tự đặc biệt
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── TC5: Đổi mật khẩu thành công ─────────────────────────────────────
    [Fact]
    public async Task Success()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            AuthProvider = (int)AuthProvider.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Old@Password1")
        };
        _mockUserRepo.Setup(r => r.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockUserRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        await _sut.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "Old@Password1",
            NewPassword     = "New@Password1"
        });

        // PasswordHash phải được cập nhật sang hash mới
        BCrypt.Net.BCrypt.Verify("New@Password1", user.PasswordHash).Should().BeTrue();
        _mockUserRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
