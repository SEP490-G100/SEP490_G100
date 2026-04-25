using Microsoft.Extensions.Configuration;
using Moq;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Validations;

namespace Nanny_BackEnd.Tests;

public class LogoutAsyncTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public LogoutAsyncTests()
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
    public async Task TokenNotFound()
    {
        _mockTokenRepo.Setup(t => t.FindByTokenAsync("invalid-token"))
                      .ReturnsAsync((RefreshToken?)null);

        var ex = await Record.ExceptionAsync(() => _sut.LogoutAsync("invalid-token"));
        Assert.Null(ex);

        _mockTokenRepo.Verify(t => t.RevokeAllForUserAsync(It.IsAny<Guid>()), Times.Never);
        _mockTokenRepo.Verify(t => t.SaveChangesAsync(),                      Times.Never);
    }

    [Fact]
    public async Task Success()
    {
        var userId = Guid.NewGuid();
        var token = new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            Token     = "valid-refresh-token",
            IsRevoked = false
        };

        _mockTokenRepo.Setup(t => t.FindByTokenAsync("valid-refresh-token"))
                      .ReturnsAsync(token);
        _mockTokenRepo.Setup(t => t.RevokeAllForUserAsync(userId))
                      .Returns(Task.CompletedTask);
        _mockTokenRepo.Setup(t => t.SaveChangesAsync())
                      .Returns(Task.CompletedTask);

        await _sut.LogoutAsync("valid-refresh-token");

        _mockTokenRepo.Verify(t => t.RevokeAllForUserAsync(userId), Times.Once);
        _mockTokenRepo.Verify(t => t.SaveChangesAsync(),             Times.Once);
    }
}
