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

public class VerifyEmailAsyncTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;
    private readonly AuthService                  _sut;

    public VerifyEmailAsyncTests()
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
        _mockOtp.Setup(o => o.ValidateAsync("test@mail.com", "999999", OtpPurpose.VerifyEmail))
                .ReturnsAsync((OtpCode?)null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.VerifyEmailAsync(new VerifyEmailRequest
        {
            Email   = "test@mail.com",
            OtpCode = "999999"
        }));
    }

    [Fact]
    public async Task Success()
    {
        var user = new User
        {
            Id             = Guid.NewGuid(),
            Email          = "pending@mail.com",
            Status         = (int)UserStatus.Pending,
            EmailConfirmed = false
        };

        _mockOtp.Setup(o => o.ValidateAsync("pending@mail.com", "111222", OtpPurpose.VerifyEmail))
                .ReturnsAsync(new OtpCode { Code = "111222", Email = "pending@mail.com" });

        _mockUserRepo.Setup(r => r.FindByEmailAsync("pending@mail.com"))
                     .ReturnsAsync(user);
        _mockUserRepo.Setup(r => r.SaveChangesAsync())
                     .Returns(Task.CompletedTask);

        await _sut.VerifyEmailAsync(new VerifyEmailRequest
        {
            Email   = "pending@mail.com",
            OtpCode = "111222"
        });

        Assert.True(user.EmailConfirmed);
        Assert.Equal((int)UserStatus.Active, user.Status);

        _mockUserRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
