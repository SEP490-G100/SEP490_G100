using Microsoft.AspNetCore.Hosting;
using Moq;
using Nanny_BackEnd.DTOs.Verification;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.VerificationRequestController"/> submit ?
/// <see cref="VerificationRequestService.NannySubmitVerificationRequestAsync"/>.
/// </summary>
public class NannySubmitVerificationRequestAsyncTests
{
    private readonly Mock<IVerificationRequestRepository> _mockRepo;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly VerificationRequestService _sut;

    public NannySubmitVerificationRequestAsyncTests()
    {
        _mockRepo = new Mock<IVerificationRequestRepository>();
        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new VerificationRequestService(_mockRepo.Object, _mockNotif.Object);
    }

    private static NannyProfile MakeNanny(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        VerificationStatus = 0,
        CreatedAt = DateTime.UtcNow,
        NannySkills = new List<NannySkill>(),
        NannyCertificates = new List<NannyCertificate>(),
        User = new User
        {
            Id = userId,
            Email = "n@n.n",
            FirstName = "A",
            LastName = "B"
        }
    };

    private static UploadedVerificationDocumentDto OneDoc() => new()
    {
        DocumentType = (int)VerificationDocumentType.IdentityCard,
        DocumentUrl = "https://x/a.pdf",
        FileName = "a.pdf",
        FileSize = 10,
        ExpiryDate = DateTime.UtcNow.Date
    };

    [Fact]
    public async Task NoNannyProfile_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync((NannyProfile?)null);

        var (ok, msg) = await _sut.NannySubmitVerificationRequestAsync(userId, new SubmitVerificationRequestDto
        {
            RequestType = (int)VerificationRequestType.ProfileVerification,
            Documents = new List<UploadedVerificationDocumentDto> { OneDoc() }
        });

        Assert.False(ok);
        Assert.Equal("Không tìm thấy hồ sơ Nanny.", msg);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NoDocuments_ReturnsFalse(bool useNull)
    {
        var userId = Guid.NewGuid();
        var np = MakeNanny(userId);
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        var body = new SubmitVerificationRequestDto
        {
            RequestType = (int)VerificationRequestType.ProfileVerification
        };
        if (useNull)
            body.Documents = null!;
        else
            body.Documents = new List<UploadedVerificationDocumentDto>();

        var (ok, msg) = await _sut.NannySubmitVerificationRequestAsync(userId, body);

        Assert.False(ok);
        Assert.Equal("Ban phai tai len it nhat mot tai lieu.", msg);
    }

    [Fact]
    public async Task PendingSameTypeExists_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var np = MakeNanny(userId);
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetRequestsByNannyProfileAsync(np.Id))
            .ReturnsAsync(new List<VerificationRequest>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    NannyProfileId = np.Id,
                    Status = (int)NannyVerificationRequestStatus.Pending,
                    RequestType = (int)VerificationRequestType.ProfileVerification,
                    CreatedAt = DateTime.UtcNow
                }
            });

        var (ok, msg) = await _sut.NannySubmitVerificationRequestAsync(userId, new SubmitVerificationRequestDto
        {
            RequestType = (int)VerificationRequestType.ProfileVerification,
            Documents = new List<UploadedVerificationDocumentDto> { OneDoc() }
        });

        Assert.False(ok);
        Assert.Equal("Bạn đã có một yêu cầu đang chờ duyệt cho loại hồ sơ này.", msg);
    }

    [Fact]
    public async Task HealthCertificateWithoutExpiryDate_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var np = MakeNanny(userId);
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetRequestsByNannyProfileAsync(np.Id))
            .ReturnsAsync(new List<VerificationRequest>());

        var (ok, msg) = await _sut.NannySubmitVerificationRequestAsync(userId, new SubmitVerificationRequestDto
        {
            RequestType = (int)VerificationRequestType.HealthCertificate,
            Documents = new List<UploadedVerificationDocumentDto>
            {
                new()
                {
                    DocumentType = (int)VerificationDocumentType.HealthCertificate,
                    DocumentUrl = "https://x/h.pdf",
                    FileName = "h.pdf",
                    FileSize = 10,
                    ExpiryDate = null
                }
            }
        });

        Assert.False(ok);
        Assert.Equal("Bạn phải chọn ngày cấp cho tài liệu xác minh.", msg);
    }

    [Fact]
    public async Task Success_ProfileRequest_AddsNotifiesSaves()
    {
        var userId = Guid.NewGuid();
        var np = MakeNanny(userId);
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetRequestsByNannyProfileAsync(np.Id))
            .ReturnsAsync(new List<VerificationRequest>());
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockNotif
            .Setup(n => n.createNotificationForModerators(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
        var body = new SubmitVerificationRequestDto
        {
            RequestType = (int)VerificationRequestType.ProfileVerification,
            Documents = new List<UploadedVerificationDocumentDto> { OneDoc() }
        };

        var (ok, msg) = await _sut.NannySubmitVerificationRequestAsync(userId, body);

        Assert.True(ok);
        Assert.Equal("Gửi yêu cầu xác minh thành công.", msg);
        Assert.Equal((int)VerificationStatus.Pending, np.VerificationStatus);
        _mockRepo.Verify(
            r => r.AddRequest(It.Is<VerificationRequest>(v =>
                v.NannyProfileId == np.Id
                && v.RequestType == (int)VerificationRequestType.ProfileVerification
                && v.Status == (int)NannyVerificationRequestStatus.Pending
                && v.VerificationDocuments.Count == 1)),
            Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockNotif.Verify(
            n => n.createNotificationForModerators(
                "Có yêu cầu xác minh mới",
                "Một nanny vừa gửi yêu cầu xác minh mới và đang chờ moderator xem xét.",
                NotificationTypes.VerificationRequestSubmitted,
                It.IsAny<Guid?>(),
                "VerificationRequest",
                userId),
            Times.Once);
    }
}
