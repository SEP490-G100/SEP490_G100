using Google.Apis.Auth;
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

// ── Subclass override ValidateGoogleTokenAsync để bypass Google SDK ───────
// (GoogleJsonWebSignature.ValidateAsync là static → không mock trực tiếp được)
public class TestableAuthService : AuthService
{
    private readonly GoogleJsonWebSignature.Payload _fakePayload;

    public TestableAuthService(
        GoogleJsonWebSignature.Payload fakePayload,
        IUserRepository         userRepo,
        IRefreshTokenRepository tokenRepo,
        JwtService             jwt,
        OtpService             otp,
        EmailService           email,
        PasswordValidator      pwdValidator,
        IConfiguration         config)
        : base(userRepo, tokenRepo, jwt, otp, email, pwdValidator, config)
    {
        _fakePayload = fakePayload;
    }

    protected override Task<GoogleJsonWebSignature.Payload> ValidateGoogleTokenAsync(string idToken)
        => Task.FromResult(_fakePayload);
}

public class GoogleLoginTests
{
    private readonly Mock<IUserRepository>         _mockUserRepo;
    private readonly Mock<IRefreshTokenRepository> _mockTokenRepo;
    private readonly Mock<JwtService>             _mockJwt;
    private readonly Mock<OtpService>             _mockOtp;
    private readonly Mock<EmailService>           _mockEmail;

    private readonly IConfiguration    _config;
    private readonly PasswordValidator _pwdValidator;

    public GoogleLoginTests()
    {
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:FromName"]     = "NannyMatch",
                ["EmailSettings:FromEmail"]    = "noreply@test.com",
                ["EmailSettings:SmtpHost"]     = "localhost",
                ["EmailSettings:SmtpPort"]     = "587",
                ["EmailSettings:SmtpUser"]     = "user",
                ["EmailSettings:SmtpPassword"] = "pass",
                ["Google:ClientId"]            = "fake-google-client-id",
            })
            .Build();

        var mockOtpRepo = new Mock<IOtpRepository>();

        _mockUserRepo  = new Mock<IUserRepository>();
        _mockTokenRepo = new Mock<IRefreshTokenRepository>();
        _mockJwt       = new Mock<JwtService>(_config);
        _mockOtp       = new Mock<OtpService>(mockOtpRepo.Object);
        _mockEmail     = new Mock<EmailService>(_config);
        _pwdValidator  = new PasswordValidator(new ConfigurationBuilder().Build());
    }

    // ── Helper tạo TestableAuthService với payload Google giả ────────────
    private TestableAuthService BuildSut(GoogleJsonWebSignature.Payload payload)
        => new(
            payload,
            _mockUserRepo.Object,
            _mockTokenRepo.Object,
            _mockJwt.Object,
            _mockOtp.Object,
            _mockEmail.Object,
            _pwdValidator,
            _config);

    // ── Helper tạo Google payload giả ────────────────────────────────────
    private static GoogleJsonWebSignature.Payload FakePayload(string email = "google@mail.com") => new()
    {
        Email      = email,
        GivenName  = "Google",
        FamilyName = "User",
        Subject    = "google-subject-id-123",
        Picture    = "https://avatar.url/photo.jpg"
    };

    // ── TC1: Email đã đăng ký bằng email/password ─────────────────────────
    [Fact]
    public async Task EmailByPassword()
    {
        var payload = FakePayload("existing@mail.com");
        var existingUser = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "existing@mail.com",
            AuthProvider = (int)AuthProvider.Email,
            Status       = (int)UserStatus.Active
        };

        _mockUserRepo.Setup(r => r.FindByEmailAsync("existing@mail.com"))
                     .ReturnsAsync(existingUser);

        var sut = BuildSut(payload);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "fake-token" }));
        Assert.Equal("Email này đã đăng ký bằng mật khẩu. Vui lòng đăng nhập bằng email.", ex.Message);
    }

    // ── TC2: User chưa tồn tại → tạo mới và đăng nhập ───────────────────
    [Fact]
    public async Task NewUser()
    {
        var payload = FakePayload("newgoogle@mail.com");

        _mockUserRepo.Setup(r => r.FindByEmailAsync("newgoogle@mail.com"))
                     .ReturnsAsync((User?)null);
        _mockUserRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockUserRepo.Setup(r => r.GetRolesAsync(It.IsAny<Guid>())).ReturnsAsync([]);

        _mockTokenRepo.Setup(t => t.RevokeAllForUserAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _mockTokenRepo.Setup(t => t.Add(It.IsAny<RefreshToken>()));
        _mockTokenRepo.Setup(t => t.SaveChangesAsync()).Returns(Task.CompletedTask);

        _mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<User>(), It.IsAny<List<string>>()))
                .Returns(("fake-access-token", DateTime.UtcNow.AddMinutes(30), "fake-jwt-id"));
        _mockJwt.Setup(j => j.GenerateRefreshToken()).Returns("fake-refresh-token");

        var sut = BuildSut(payload);
        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "fake-token" });

        Assert.NotNull(response);
        Assert.Equal("fake-access-token", response.AccessToken);
        Assert.Equal("newgoogle@mail.com", response.User.Email);
        Assert.Equal("google", response.User.AuthProvider);

        // Xác nhận user được lưu vào DB
        _mockUserRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // ── TC3: User đã có Google account → đăng nhập thành công ────────────
    [Fact]
    public async Task ExistingUser()
    {
        var payload = FakePayload("google@mail.com");
        var existingUser = new User
        {
            Id           = Guid.NewGuid(),
            Email        = "google@mail.com",
            FirstName    = "Google",
            LastName     = "User",
            AuthProvider = (int)AuthProvider.Google,
            Status       = (int)UserStatus.Active
        };

        _mockUserRepo.Setup(r => r.FindByEmailAsync("google@mail.com"))
                     .ReturnsAsync(existingUser);
        _mockUserRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockUserRepo.Setup(r => r.GetRolesAsync(existingUser.Id)).ReturnsAsync(["Nanny"]);

        _mockTokenRepo.Setup(t => t.RevokeAllForUserAsync(existingUser.Id)).Returns(Task.CompletedTask);
        _mockTokenRepo.Setup(t => t.Add(It.IsAny<RefreshToken>()));
        _mockTokenRepo.Setup(t => t.SaveChangesAsync()).Returns(Task.CompletedTask);

        _mockJwt.Setup(j => j.GenerateAccessToken(existingUser, It.IsAny<List<string>>()))
                .Returns(("google-access-token", DateTime.UtcNow.AddMinutes(30), "jwt-id"));
        _mockJwt.Setup(j => j.GenerateRefreshToken()).Returns("google-refresh-token");

        var sut = BuildSut(payload);
        var response = await sut.GoogleLoginAsync(new GoogleLoginRequest { IdToken = "fake-token" });

        Assert.NotNull(response);
        Assert.Equal("google-access-token", response.AccessToken);
        Assert.Contains("Nanny", response.User.Roles);
    }
}
