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
/// VerificationRequestController.ModeratorReviewVerification ->
/// VerificationRequestService.ModeratorReviewVerificationAsync
/// </summary>
public class ModeratorReviewVerificationAsyncTests
{
    private readonly Mock<IVerificationRequestRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly VerificationRequestService _sut;

    public ModeratorReviewVerificationAsyncTests()
    {
        _mockRepo = new Mock<IVerificationRequestRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new VerificationRequestService(_mockRepo.Object, _mockNotif.Object);
    }

    private static VerificationRequest MakePending(
        Guid id,
        int status = (int)NannyVerificationRequestStatus.Pending,
        int requestType = (int)VerificationRequestType.ProfileVerification)
    {
        var userId = Guid.NewGuid();
        var nannyProfileId = Guid.NewGuid();
        return new VerificationRequest
        {
            Id = id,
            NannyProfileId = nannyProfileId,
            RequestType = requestType,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            NannyProfile = new NannyProfile
            {
                Id = nannyProfileId,
                UserId = userId,
                VerificationStatus = (int)VerificationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                NannySkills = new List<NannySkill>(),
                NannyCertificates = new List<NannyCertificate>(),
                User = new User
                {
                    Id = userId,
                    FirstName = "A",
                    LastName = "B",
                    Email = "n@n.n"
                }
            },
            VerificationDocuments = new List<VerificationDocument>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DocumentType = (int)VerificationDocumentType.HealthCertificate,
                    DocumentUrl = "https://x/h.pdf",
                    FileName = "h.pdf",
                    IsDeleted = false
                }
            }
        };
    }

    private void StubSaveAndNotify()
    {
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockNotif
            .Setup(n => n.createNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
    }

    // Condition: action not in {2,3}.
    [Fact]
    public async Task InvalidAction_Returns400()
    {
        var id = Guid.NewGuid();
        var r = await _sut.ModeratorReviewVerificationAsync(id, Guid.NewGuid(), new ReviewVerificationRequest
        {
            Action = 0
        });

        Assert.False(r.Success);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Action không hợp lệ. Chỉ chấp nhận 2 (Approve) hoặc 3 (Reject).", r.Message);
        _mockRepo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: reject without reason.
    [Fact]
    public async Task RejectWithoutReason_Returns400()
    {
        var id = Guid.NewGuid();
        var r = await _sut.ModeratorReviewVerificationAsync(id, Guid.NewGuid(), new ReviewVerificationRequest
        {
            Action = (int)NannyVerificationRequestStatus.Rejected,
            RejectionReason = "   "
        });

        Assert.False(r.Success);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Lý do từ chối là bắt buộc khi từ chối yêu cầu.", r.Message);
    }

    // Condition: request not found.
    [Fact]
    public async Task NotFound_Returns404()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(x => x.GetByIdAsync(id)).ReturnsAsync((VerificationRequest?)null);

        var r = await _sut.ModeratorReviewVerificationAsync(id, Guid.NewGuid(), new ReviewVerificationRequest
        {
            Action = (int)NannyVerificationRequestStatus.Approved
        });

        Assert.False(r.Success);
        Assert.Equal(404, r.StatusCode);
        Assert.Equal("Không tìm thấy yêu cầu xác minh.", r.Message);
    }

    // Condition: request already processed (status != Pending).
    [Fact]
    public async Task NotPending_Returns409()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(x => x.GetByIdAsync(id))
            .ReturnsAsync(MakePending(id, (int)NannyVerificationRequestStatus.Approved));

        var r = await _sut.ModeratorReviewVerificationAsync(id, Guid.NewGuid(), new ReviewVerificationRequest
        {
            Action = (int)NannyVerificationRequestStatus.Approved
        });

        Assert.False(r.Success);
        Assert.Equal(409, r.StatusCode);
        Assert.Equal("Yêu cầu này đã được xử lý trước đó.", r.Message);
    }

    // Condition: approve -> update request/profile/health-expiry + notification.
    [Fact]
    public async Task Approve_Success_UpdatesNannyAndHealthExpiry()
    {
        var id = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var vr = MakePending(id);

        _mockRepo.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(vr);
        _mockRepo.Setup(x => x.GetNannyProfileAsync(vr.NannyProfileId)).ReturnsAsync(vr.NannyProfile);
        StubSaveAndNotify();

        var r = await _sut.ModeratorReviewVerificationAsync(id, moderatorId, new ReviewVerificationRequest
        {
            Action = (int)NannyVerificationRequestStatus.Approved
        });

        Assert.True(r.Success);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal("Đã duyệt yêu cầu xác minh.", r.Message);
        Assert.Equal((int)NannyVerificationRequestStatus.Approved, vr.Status);
        Assert.Equal((int)VerificationStatus.Approved, vr.NannyProfile.VerificationStatus);
        Assert.Equal(moderatorId, vr.NannyProfile.VerifiedBy);

        var health = vr.VerificationDocuments.First(d => d.DocumentType == (int)VerificationDocumentType.HealthCertificate);
        Assert.NotNull(health.ExpiryDate);

        _mockNotif.Verify(n => n.createNotification(
            vr.NannyProfile.UserId,
            "Yêu cầu xác minh của bạn đã được chấp thuận",
            "Moderator đã chấp thuận yêu cầu xác minh của bạn.",
            NotificationTypes.VerificationRequestApproved,
            id,
            "VerificationRequest",
            moderatorId), Times.Once);

        _mockRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    // Condition: reject -> update request/profile + notification with reason.
    [Fact]
    public async Task Reject_Success_NotifiesWithReason()
    {
        var id = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var vr = MakePending(id);

        _mockRepo.Setup(x => x.GetByIdAsync(id)).ReturnsAsync(vr);
        _mockRepo.Setup(x => x.GetNannyProfileAsync(vr.NannyProfileId)).ReturnsAsync(vr.NannyProfile);
        StubSaveAndNotify();

        var r = await _sut.ModeratorReviewVerificationAsync(id, moderatorId, new ReviewVerificationRequest
        {
            Action = (int)NannyVerificationRequestStatus.Rejected,
            RejectionReason = "  giay to mo  "
        });

        Assert.True(r.Success);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal("Đã từ chối yêu cầu xác minh.", r.Message);
        Assert.Equal("giay to mo", vr.RejectionReason);
        Assert.Equal((int)VerificationStatus.Rejected, vr.NannyProfile.VerificationStatus);
        Assert.Null(vr.NannyProfile.VerifiedBy);

        _mockNotif.Verify(n => n.createNotification(
            vr.NannyProfile.UserId,
            "Yêu cầu xác minh của bạn đã bị từ chối",
            "Moderator đã từ chối yêu cầu xác minh của bạn. Lý do: giay to mo",
            NotificationTypes.VerificationRequestRejected,
            id,
            "VerificationRequest",
            moderatorId), Times.Once);
    }
}
