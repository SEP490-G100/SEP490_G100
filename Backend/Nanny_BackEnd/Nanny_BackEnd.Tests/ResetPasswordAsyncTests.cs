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

public class ResetPasswordAsyncTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public ResetPasswordAsyncTests()
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

    [Fact]
    public async Task InvalidOtp()
    {
        _mockOtp.Setup(o => o.ValidateAsync("user@mail.com", "000000", OtpPurpose.ForgotPassword))
                .ReturnsAsync((OtpCode?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email       = "user@mail.com",
            OtpCode     = "000000",
            NewPassword = "New@Password1"
        }));
    }

    [Fact]
    public async Task GoogleAccount()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "google@mail.com",
            AuthProvider = (int)AuthProvider.Google
        };

        _mockOtp.Setup(o => o.ValidateAsync("google@mail.com", "123456", OtpPurpose.ForgotPassword))
                .ReturnsAsync(new OtpCode { Code = "123456", Email = "google@mail.com" });
        _mockUserRepo.Setup(r => r.FindByEmailAsync("google@mail.com"))
                     .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email       = "google@mail.com",
            OtpCode     = "123456",
            NewPassword = "New@Password1"
        }));
    }

    [Fact]
    public async Task WeakNewPassword()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "user@mail.com",
            AuthProvider = (int)AuthProvider.Email
        };

        _mockOtp.Setup(o => o.ValidateAsync("user@mail.com", "123456", OtpPurpose.ForgotPassword))
                .ReturnsAsync(new OtpCode { Code = "123456", Email = "user@mail.com" });
        _mockUserRepo.Setup(r => r.FindByEmailAsync("user@mail.com"))
                     .ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email       = "user@mail.com",
            OtpCode     = "123456",
            NewPassword = "weak"        }));
    }

    [Fact]
    public async Task Success()
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "user@mail.com",
            AuthProvider = (int)AuthProvider.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Old@Password1")
        };

        _mockOtp.Setup(o => o.ValidateAsync("user@mail.com", "123456", OtpPurpose.ForgotPassword))
                .ReturnsAsync(new OtpCode { Code = "123456", Email = "user@mail.com" });
        _mockUserRepo.Setup(r => r.FindByEmailAsync("user@mail.com"))
                     .ReturnsAsync(user);
        _mockUserRepo.Setup(r => r.SaveChangesAsync())
                     .Returns(Task.CompletedTask);

        await _sut.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email       = "user@mail.com",
            OtpCode     = "123456",
            NewPassword = "New@Password1"
        });

        // PasswordHash phải được cập nhật sang hash mới
        Assert.True(BCrypt.Net.BCrypt.Verify("New@Password1", user.PasswordHash));
        _mockUserRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
