using Microsoft.AspNetCore.Hosting;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.NannyVerificationRequestController"/> detail →
/// <see cref="VerificationRequestService.NannyViewVerificationRequestDetailAsync"/>.
/// </summary>
public class NannyViewVerificationRequestDetailAsyncTests
{
    private const string NoNannyProfileMessage = "Khong tim thay ho so Nanny.";
    private const string NoRequestMessage = "Khong tim thay yeu cau xac minh.";

    private readonly Mock<IVerificationRequestRepository> _mockRepo;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly VerificationRequestService _sut;

    public NannyViewVerificationRequestDetailAsyncTests()
    {
        _mockRepo = new Mock<IVerificationRequestRepository>();
        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new VerificationRequestService(_mockRepo.Object, _mockEnv.Object, _mockNotif.Object);
    }

    private static (NannyProfile Profile, VerificationRequest Vr) MakeOwnedPair(Guid userId, Guid? vrId = null, Guid? npId = null)
    {
        var pid = npId ?? Guid.NewGuid();
        var uid = userId;
        var rid = vrId ?? Guid.NewGuid();
        var np = new NannyProfile
        {
            Id = pid,
            UserId = uid,
            SalaryType = 0,
            VerificationStatus = 0,
            CreatedAt = DateTime.UtcNow,
            NannySkills = new List<NannySkill>(),
            NannyCertificates = new List<NannyCertificate>(),
            User = new User
            {
                Id = uid,
                Email = "a@a.a",
                FirstName = "F",
                LastName = "L"
            }
        };
        var vr = new VerificationRequest
        {
            Id = rid,
            NannyProfileId = pid,
            NannyProfile = np,
            RequestType = 1,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            VerificationDocuments = new List<VerificationDocument>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DocumentType = 0,
                    DocumentUrl = "https://x/x.pdf",
                    FileName = "x.pdf",
                    FileSize = 1,
                    IsDeleted = false
                }
            }
        };
        return (np, vr);
    }

    // Condition: user chưa có hồ sơ nanny.
    [Fact]
    public async Task NoNannyProfile_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync((NannyProfile?)null);

        var (ok, data, message) = await _sut.NannyViewVerificationRequestDetailAsync(userId, id);

        Assert.False(ok);
        Assert.Null(data);
        Assert.Equal(NoNannyProfileMessage, message);
        _mockRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: GetById null.
    [Fact]
    public async Task RequestMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var (np, _) = MakeOwnedPair(userId);
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((VerificationRequest?)null);

        var (ok, data, message) = await _sut.NannyViewVerificationRequestDetailAsync(userId, id);

        Assert.False(ok);
        Assert.Null(data);
        Assert.Equal(NoRequestMessage, message);
    }

    // Condition: yêu cầu thuộc hồ sơ khác (NannyProfileId ≠ profile hiện tại).
    [Fact]
    public async Task RequestBelongsToOtherProfile_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var (np, vr) = MakeOwnedPair(userId, rid);
        vr.NannyProfileId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetByIdAsync(rid)).ReturnsAsync(vr);

        var (ok, data, message) = await _sut.NannyViewVerificationRequestDetailAsync(userId, rid);

        Assert.False(ok);
        Assert.Null(data);
        Assert.Equal(NoRequestMessage, message);
    }

    // Condition: hợp lệ — map MapDetailDto.
    [Fact]
    public async Task Success_ReturnsDetail()
    {
        var userId = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var (np, vr) = MakeOwnedPair(userId, rid);
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetByIdAsync(rid)).ReturnsAsync(vr);

        var (ok, data, message) = await _sut.NannyViewVerificationRequestDetailAsync(userId, rid);

        Assert.True(ok);
        Assert.Null(message);
        Assert.NotNull(data);
        Assert.Equal(rid, data!.Id);
        Assert.Equal("F", data.NannyFirstName);
        Assert.Equal("L", data.NannyLastName);
        Assert.Single(data.Documents);
    }
}
