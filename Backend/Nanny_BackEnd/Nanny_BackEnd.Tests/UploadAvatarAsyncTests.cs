using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ProfileController.UploadAvatar"/> →
/// <see cref="ProfileService.UploadAvatarAsync"/>.
/// </summary>
public class UploadAvatarAsyncTests
{
    private const string UserNotFoundMessage = "NgÆ°á»i dÃ¹ng khÃ´ng tá»“n táº¡i.";

    private const string ExtNotAllowedMessage =
        "Chá»‰ cháº¥p nháº­n file áº£nh .jpg, .jpeg hoáº·c .png.";

    private const string ContentTypeNotAllowedMessage =
        "Chá»‰ cháº¥p nháº­n áº£nh JPEG/PNG há»£p lá»‡.";

    private const string FileTooLargeMessage =
        "File áº£nh khÃ´ng Ä‘Æ°á»£c vÆ°á»£t quÃ¡ 5MB.";

    private readonly Mock<IUserRepository> _mockUser;
    private readonly Mock<IParentRepository> _mockParent;
    private readonly Mock<IChildRepository> _mockChild;
    private readonly Mock<INannyProfileRepository> _mockNannyProfile;
    private readonly Mock<INannySkillRepository> _mockNannySkill;
    private readonly Mock<INannyCertificateRepository> _mockNannyCert;
    private readonly Mock<INannyAvailabilityRepository> _mockNannyAvail;
    private readonly Mock<IVerificationRequestRepository> _mockVerification;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<IGeocodingService> _mockGeo;
    private readonly Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory> _mockScope;
    private readonly ProfileService _sut;

    public UploadAvatarAsyncTests()
    {
        _mockUser = new Mock<IUserRepository>();
        _mockParent = new Mock<IParentRepository>();
        _mockChild = new Mock<IChildRepository>();
        _mockNannyProfile = new Mock<INannyProfileRepository>();
        _mockNannySkill = new Mock<INannySkillRepository>();
        _mockNannyCert = new Mock<INannyCertificateRepository>();
        _mockNannyAvail = new Mock<INannyAvailabilityRepository>();
        _mockVerification = new Mock<IVerificationRequestRepository>();
        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockGeo = new Mock<IGeocodingService>();
        _mockScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        _sut = new ProfileService(
            _mockUser.Object,
            _mockParent.Object,
            _mockChild.Object,
            _mockNannyProfile.Object,
            _mockNannySkill.Object,
            _mockNannyCert.Object,
            _mockNannyAvail.Object,
            _mockVerification.Object,
            _mockEnv.Object,
            _mockGeo.Object,
            _mockScope.Object,
            NullLogger<ProfileService>.Instance);
    }

    private static IFormFile MakeFile(string fileName, string? contentType, long length)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.ContentType).Returns(() => contentType!);
        mock.Setup(f => f.Length).Returns(length);
        mock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream dest, CancellationToken _) =>
            {
                var buffer = new byte[8192];
                for (var remaining = length; remaining > 0;)
                {
                    var n = (int)Math.Min(remaining, buffer.Length);
                    dest.Write(buffer, 0, n);
                    remaining -= n;
                }

                return Task.CompletedTask;
            });
        return mock.Object;
    }

    private static User UserEntity(Guid id) => new()
    {
        Id = id,
        Email = "u@test.local",
        FirstName = "A",
        LastName = "B"
    };

    // Condition: user không tồn tại.
    [Fact]
    public async Task UserNotFound_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((User?)null);
        var file = MakeFile("a.jpg", "image/jpeg", 10);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UploadAvatarAsync(id, file));

        Assert.Equal(UserNotFoundMessage, ex.Message);
    }

    // Condition: phần mở rộng không nằm trong { .jpg, .jpeg, .png }.
    [Fact]
    public async Task DisallowedExtension_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(UserEntity(id));
        var file = MakeFile("x.gif", "image/gif", 10);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UploadAvatarAsync(id, file));

        Assert.Equal(ExtNotAllowedMessage, ex.Message);
        _mockUser.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // Condition: phần mở rộng OK nhưng Content-Type không phải image/jpeg|image/png.
    [Fact]
    public async Task DisallowedContentType_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(UserEntity(id));
        var file = MakeFile("x.jpg", "image/gif", 10);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UploadAvatarAsync(id, file));

        Assert.Equal(ContentTypeNotAllowedMessage, ex.Message);
        _mockUser.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // Condition: ContentType null/empty — không hợp lệ.
    [Fact]
    public async Task NullOrEmptyContentType_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(UserEntity(id));
        var file = MakeFile("x.png", null, 10);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UploadAvatarAsync(id, file));

        Assert.Equal(ContentTypeNotAllowedMessage, ex.Message);
    }

    // Condition: dung lượng > 5MB.
    [Fact]
    public async Task ExceedsMaxSize_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(UserEntity(id));
        var over = 5L * 1024 * 1024 + 1;
        var file = MakeFile("x.jpg", "image/jpeg", over);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UploadAvatarAsync(id, file));

        Assert.Equal(FileTooLargeMessage, ex.Message);
        _mockUser.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // Condition: lưu file dưới wwwroot/uploads/avatars, cập nhật user, trả URL có query t=.
    [Fact]
    public async Task Success_WritesFile_UpdatesUser_ReturnsUrl()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), "nanny-avatar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);
        try
        {
            _mockEnv.Setup(e => e.WebRootPath).Returns(webRoot);
            var id = Guid.NewGuid();
            var user = UserEntity(id);
            _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
            _mockUser.Setup(u => u.SaveChangesAsync()).Returns(Task.CompletedTask);
            var file = MakeFile("Photo.JPEG", "image/jpeg", 128);

            var url = await _sut.UploadAvatarAsync(id, file);

            var expectedName = $"{id}.jpeg";
            var path = Path.Combine(webRoot, "uploads", "avatars", expectedName);
            Assert.True(File.Exists(path), $"Expected file at {path}");
            Assert.Equal(user.AvatarUrl, url);
            Assert.Contains($"/uploads/avatars/{expectedName}?t=", url, StringComparison.Ordinal);
            Assert.Equal(id, user.UpdatedBy);
            Assert.NotNull(user.UpdatedAt);
            _mockUser.Verify(u => u.SaveChangesAsync(), Times.Once);
        }
        finally
        {
            try
            {
                if (Directory.Exists(webRoot))
                    Directory.Delete(webRoot, recursive: true);
            }
            catch
            {
                // best-effort cleanup on temp dir
            }
        }
    }
}
