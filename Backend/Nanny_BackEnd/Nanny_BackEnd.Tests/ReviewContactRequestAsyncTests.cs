using System.Text.Json;
using Moq;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="NanniesController.ReviewContactRequest"/> ? <see cref="ContactRequestService.ReviewAsync"/>.
/// </summary>
public class ReviewContactRequestAsyncTests
{
    private readonly Mock<IContactRequestRepository> _mockCr;
    private readonly Mock<IParentRepository>         _mockParent;
    private readonly Mock<INannyProfileRepository>  _mockNanny;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly ContactRequestService          _sut;

    public ReviewContactRequestAsyncTests()
    {
        _mockCr     = new Mock<IContactRequestRepository>();
        _mockParent = new Mock<IParentRepository>();
        _mockNanny  = new Mock<INannyProfileRepository>();
        _mockNotif  = new Mock<INotificationService>();
        _sut = new ContactRequestService(_mockCr.Object, _mockParent.Object, _mockNanny.Object, _mockNotif.Object);
    }

    private static string ErrorMessage(object body) =>
        JsonDocument.Parse(JsonSerializer.Serialize(body)).RootElement.GetProperty("message").GetString()!;

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task InvalidAction_Returns400(int action)
    {
        var r = await _sut.ReviewAsync(Guid.NewGuid(), Guid.NewGuid(), action, null);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Action không hợp lệ. Dùng 1 (accept) hoặc 2 (reject).", ErrorMessage(r.Body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task RejectWithoutReason_Returns400(string? responseMessage)
    {
        var r = await _sut.ReviewAsync(Guid.NewGuid(), Guid.NewGuid(), 2, responseMessage);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Vui lòng nhập lý do khi từ chối request contact.", ErrorMessage(r.Body));
    }

    [Fact]
    public async Task ResponseMessageTooLong_Returns400()
    {
        var r = await _sut.ReviewAsync(Guid.NewGuid(), Guid.NewGuid(), 1, new string('x', 1001));

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Nội dung phản hồi không được vượt quá 1000 ký tự.", ErrorMessage(r.Body));
    }

    [Fact]
    public async Task NotNanny_Returns400()
    {
        var userId = Guid.NewGuid();
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId)).ReturnsAsync((NannyProfile?)null);

        var r = await _sut.ReviewAsync(userId, Guid.NewGuid(), 1, null);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Tài khoản không phải nanny.", ErrorMessage(r.Body));
        _mockCr.Verify(
            c => c.GetByIdForNannyReviewTrackingAsync(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestNotFound_Returns404()
    {
        var userId  = Guid.NewGuid();
        var crId    = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetByIdForNannyReviewTrackingAsync(crId, nannyId))
            .ReturnsAsync((ContactRequest?)null);

        var r = await _sut.ReviewAsync(userId, crId, 1, null);

        Assert.Equal(404, r.StatusCode);
        Assert.Equal("Không tìm thấy request contact hoặc bạn không có quyền xử lý.", ErrorMessage(r.Body));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AlreadyProcessed_Returns400(int doneStatus)
    {
        var userId  = Guid.NewGuid();
        var crId    = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetByIdForNannyReviewTrackingAsync(crId, nannyId))
            .ReturnsAsync(new ContactRequest { Id = crId, Status = doneStatus, NannyProfileId = nannyId });

        var r = await _sut.ReviewAsync(userId, crId, 1, null);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Request contact này đã được xử lý trước đó.", ErrorMessage(r.Body));
    }

    [Fact]
    public async Task NonPending_OtherCode_Returns400()
    {
        var userId  = Guid.NewGuid();
        var crId    = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetByIdForNannyReviewTrackingAsync(crId, nannyId))
            .ReturnsAsync(new ContactRequest { Id = crId, Status = 5, NannyProfileId = nannyId });

        var r = await _sut.ReviewAsync(userId, crId, 1, null);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Chỉ request contact đang chờ duyệt mới có thể xử lý.", ErrorMessage(r.Body));
    }

    [Fact]
    public async Task Success_Accept_SavesAndNotifiesParent()
    {
        var userId   = Guid.NewGuid();
        var parentU  = Guid.NewGuid();
        var crId     = Guid.NewGuid();
        var nannyId  = Guid.NewGuid();
        var ppId     = Guid.NewGuid();
        var cr = new ContactRequest
        {
            Id              = crId,
            Status          = 0,
            ParentProfileId = ppId,
            NannyProfileId  = nannyId,
            Message         = "hi",
            ParentProfile   = new ParentProfile { Id = ppId, UserId = parentU, User = new User { Id = parentU } },
            NannyProfile    = new NannyProfile
            {
                Id     = nannyId,
                UserId = userId,
                User   = new User { Id = userId, FirstName = "N", LastName = "1" }
            }
        };
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetByIdForNannyReviewTrackingAsync(crId, nannyId)).ReturnsAsync(cr);
        _mockCr.Setup(c => c.SaveChangesAsync()).Returns(Task.CompletedTask);

        var r = await _sut.ReviewAsync(userId, crId, 1, null);

        Assert.Equal(200, r.StatusCode);
        _mockCr.Verify(c => c.SaveChangesAsync(), Times.Once);
        Assert.Equal(1, cr.Status);
        Assert.NotNull(cr.RespondedAt);
        _mockNotif.Verify(n => n.createNotification(
            parentU,
            "Request contact đã được chấp nhận",
            "N 1 đã chấp nhận request contact của bạn.",
            NotificationTypes.ContactRequestAccepted,
            crId,
            "ContactRequest",
            userId), Times.Once);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(r.Body)).RootElement;
        Assert.Equal("Bạn đã chấp nhận request contact.", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Success_Reject_SavesAndNotifiesWithReason()
    {
        var userId  = Guid.NewGuid();
        var parentU = Guid.NewGuid();
        var crId    = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        var ppId    = Guid.NewGuid();
        var cr = new ContactRequest
        {
            Id              = crId,
            Status          = 0,
            ParentProfileId = ppId,
            NannyProfileId  = nannyId,
            ParentProfile   = new ParentProfile { Id = ppId, UserId = parentU, User = new User { Id = parentU } },
            NannyProfile    = new NannyProfile
            {
                Id     = nannyId,
                UserId = userId,
                User   = new User { Id = userId, FirstName = "A", LastName = "B" }
            }
        };
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetByIdForNannyReviewTrackingAsync(crId, nannyId)).ReturnsAsync(cr);
        _mockCr.Setup(c => c.SaveChangesAsync()).Returns(Task.CompletedTask);

        var r = await _sut.ReviewAsync(userId, crId, 2, "  full  ");

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(2, cr.Status);
        Assert.Equal("full", cr.ResponseMessage);
        _mockNotif.Verify(n => n.createNotification(
            parentU,
            "Request contact bị từ chối",
            "A B đã từ chối request contact của bạn. Lý do: full",
            NotificationTypes.ContactRequestRejected,
            crId,
            "ContactRequest",
            userId), Times.Once);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(r.Body)).RootElement;
        Assert.Equal("Bạn đã từ chối request contact.", root.GetProperty("message").GetString());
    }
}
