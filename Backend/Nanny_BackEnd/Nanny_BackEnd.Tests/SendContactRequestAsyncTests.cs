using System.Text.Json;
using Moq;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="NanniesController.SendContactRequest"/> ? <see cref="ContactRequestService.SendAsync"/>.
/// </summary>
public class SendContactRequestAsyncTests
{
    private readonly Mock<IContactRequestRepository> _mockCr;
    private readonly Mock<IParentRepository>        _mockParent;
    private readonly Mock<INannyProfileRepository> _mockNanny;
    private readonly Mock<INotificationService>   _mockNotif;
    private readonly ContactRequestService         _sut;

    public SendContactRequestAsyncTests()
    {
        _mockCr     = new Mock<IContactRequestRepository>();
        _mockParent = new Mock<IParentRepository>();
        _mockNanny  = new Mock<INannyProfileRepository>();
        _mockNotif  = new Mock<INotificationService>();
        _sut = new ContactRequestService(_mockCr.Object, _mockParent.Object, _mockNanny.Object, _mockNotif.Object);
    }

    private static ParentProfile MakeParent(Guid userId) => new()
    {
        Id     = Guid.NewGuid(),
        UserId = userId,
        User   = new User { Id = userId, FirstName = "P", LastName = "A" }
    };

    private static NannyProfile MakeNanny(Guid profileId, Guid nannyUserId) => new()
    {
        Id     = profileId,
        UserId = nannyUserId,
        User   = new User { Id = nannyUserId, FirstName = "N", LastName = "1" }
    };

    private static string ErrorMessage(object body) =>
        JsonDocument.Parse(JsonSerializer.Serialize(body)).RootElement.GetProperty("message").GetString()!;

    // Confirmation: 400.
    [Fact]
    public async Task NotParent_Returns400()
    {
        var userId = Guid.NewGuid();
        _mockParent.Setup(p => p.FindByUserIdWithUserAsync(userId)).ReturnsAsync((ParentProfile?)null);

        var r = await _sut.SendAsync(userId, Guid.NewGuid(), "hi");

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Tai khoan khong phai parent.", ErrorMessage(r.Body));
    }

    // Confirmation: 404.
    [Fact]
    public async Task NannyNotFound_Returns404()
    {
        var userId  = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        _mockParent.Setup(p => p.FindByUserIdWithUserAsync(userId)).ReturnsAsync(MakeParent(userId));
        _mockNanny.Setup(n => n.FindByIdWithUserAsync(nannyId)).ReturnsAsync((NannyProfile?)null);

        var r = await _sut.SendAsync(userId, nannyId, null);

        Assert.Equal(404, r.StatusCode);
        Assert.Equal("Khong tim thay ho so nanny.", ErrorMessage(r.Body));
    }

    [Fact]
    public async Task SelfRequest_Returns400()
    {
        var userId = Guid.NewGuid();
        var nId    = Guid.NewGuid();
        _mockParent.Setup(p => p.FindByUserIdWithUserAsync(userId)).ReturnsAsync(MakeParent(userId));
        _mockNanny.Setup(n => n.FindByIdWithUserAsync(nId)).ReturnsAsync(MakeNanny(nId, userId));

        var r = await _sut.SendAsync(userId, nId, "x");

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Ban khong the gui request contact cho chinh minh.", ErrorMessage(r.Body));
    }

    // Confirmation: 400.
    [Fact]
    public async Task MessageTooLong_Returns400()
    {
        var userId  = Guid.NewGuid();
        var nUserId = Guid.NewGuid();
        var nId     = Guid.NewGuid();
        _mockParent.Setup(p => p.FindByUserIdWithUserAsync(userId)).ReturnsAsync(MakeParent(userId));
        _mockNanny.Setup(n => n.FindByIdWithUserAsync(nId)).ReturnsAsync(MakeNanny(nId, nUserId));

        var r = await _sut.SendAsync(userId, nId, new string('a', 1001));

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Noi dung request contact khong duoc vuot qua 1000 ky tu.", ErrorMessage(r.Body));
    }

    // Confirmation: 409.
    [Fact]
    public async Task PendingDuplicate_Returns409()
    {
        var userId  = Guid.NewGuid();
        var nUserId = Guid.NewGuid();
        var nId     = Guid.NewGuid();
        var parent  = MakeParent(userId);
        _mockParent.Setup(p => p.FindByUserIdWithUserAsync(userId)).ReturnsAsync(parent);
        _mockNanny.Setup(n => n.FindByIdWithUserAsync(nId)).ReturnsAsync(MakeNanny(nId, nUserId));
        _mockCr.Setup(c => c.FindByParentAndNannyNotDeletedAsync(parent.Id, nId))
            .ReturnsAsync(new ContactRequest
            {
                Id               = Guid.NewGuid(),
                ParentProfileId  = parent.Id,
                NannyProfileId   = nId,
                Status           = 0,
                Message          = "old"
            });

        var r = await _sut.SendAsync(userId, nId, "new");

        Assert.Equal(409, r.StatusCode);
        Assert.Equal(
            "Ban da gui request contact den nanny nay va dang cho phan hoi.",
            ErrorMessage(r.Body));
        _mockCr.Verify(c => c.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Success_New_CreatesRequest_NotifiesNanny()
    {
        var userId  = Guid.NewGuid();
        var nUserId = Guid.NewGuid();
        var nId     = Guid.NewGuid();
        var parent  = MakeParent(userId);
        _mockParent.Setup(p => p.FindByUserIdWithUserAsync(userId)).ReturnsAsync(parent);
        _mockNanny.Setup(n => n.FindByIdWithUserAsync(nId)).ReturnsAsync(MakeNanny(nId, nUserId));
        _mockCr.Setup(c => c.FindByParentAndNannyNotDeletedAsync(parent.Id, nId))
            .ReturnsAsync((ContactRequest?)null);
        _mockCr.Setup(c => c.SaveChangesAsync()).Returns(Task.CompletedTask);

        var r = await _sut.SendAsync(userId, nId, "  Chao  ");

        Assert.Equal(200, r.StatusCode);
        _mockCr.Verify(c => c.Add(It.Is<ContactRequest>(x =>
            x.ParentProfileId == parent.Id
            && x.NannyProfileId == nId
            && x.Message == "Chao"
            && x.Status == 0
            && x.CreatedBy == userId
            && !x.IsDeleted)), Times.Once);
        _mockCr.Verify(c => c.SaveChangesAsync(), Times.Once);
        _mockNotif.Verify(n => n.createNotification(
            nUserId,
            "Ban vua nhan duoc request contact",
            "P A vua gui request contact cho ho so cua ban.",
            NotificationTypes.ContactRequestReceived,
            It.IsAny<Guid?>(),
            "ContactRequest",
            userId), Times.Once);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(r.Body)).RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        var msg = root.GetProperty("message").GetString();
        Assert.Equal("Ban da gui request contact thanh cong. Vui long cho nanny phan hoi.", msg);
    }

    [Fact]
    public async Task Success_Resubmits_WhenPreviousNotPending()
    {
        var userId  = Guid.NewGuid();
        var nUserId = Guid.NewGuid();
        var nId     = Guid.NewGuid();
        var parent  = MakeParent(userId);
        var existing = new ContactRequest
        {
            Id               = Guid.NewGuid(),
            ParentProfileId  = parent.Id,
            NannyProfileId   = nId,
            Status           = 2,
            Message          = "old",
            ResponseMessage  = "no",
            RespondedAt      = DateTime.UtcNow,
            IsDeleted        = false
        };
        _mockParent.Setup(p => p.FindByUserIdWithUserAsync(userId)).ReturnsAsync(parent);
        _mockNanny.Setup(n => n.FindByIdWithUserAsync(nId)).ReturnsAsync(MakeNanny(nId, nUserId));
        _mockCr.Setup(c => c.FindByParentAndNannyNotDeletedAsync(parent.Id, nId))
            .ReturnsAsync(existing);
        _mockCr.Setup(c => c.SaveChangesAsync()).Returns(Task.CompletedTask);

        var r = await _sut.SendAsync(userId, nId, "lai ne");

        Assert.Equal(200, r.StatusCode);
        _mockCr.Verify(c => c.Add(It.IsAny<ContactRequest>()), Times.Never);
        _mockCr.Verify(c => c.SaveChangesAsync(), Times.Once);
        Assert.Equal(0, existing.Status);
        Assert.Equal("lai ne", existing.Message);
        Assert.Null(existing.ResponseMessage);
        Assert.Null(existing.RespondedAt);

        var root = JsonDocument.Parse(JsonSerializer.Serialize(r.Body)).RootElement;
        var msg = root.GetProperty("message").GetString();
        Assert.Equal("Ban da gui lai request contact. Vui long cho nanny phan hoi.", msg);
    }
}
