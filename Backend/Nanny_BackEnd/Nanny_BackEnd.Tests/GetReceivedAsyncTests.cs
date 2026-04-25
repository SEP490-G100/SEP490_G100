using System.Text.Json;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="NanniesController.GetReceivedContactRequests"/> ? <see cref="ContactRequestService.GetReceivedAsync"/>.
/// </summary>
public class GetReceivedAsyncTests
{
    private readonly Mock<IContactRequestRepository> _mockCr;
    private readonly Mock<IParentRepository>         _mockParent;
    private readonly Mock<INannyProfileRepository>  _mockNanny;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly ContactRequestService          _sut;

    public GetReceivedAsyncTests()
    {
        _mockCr     = new Mock<IContactRequestRepository>();
        _mockParent = new Mock<IParentRepository>();
        _mockNanny  = new Mock<INannyProfileRepository>();
        _mockNotif  = new Mock<INotificationService>();
        _sut = new ContactRequestService(_mockCr.Object, _mockParent.Object, _mockNanny.Object, _mockNotif.Object);
    }

    private static string ErrorMessage(object body) =>
        JsonDocument.Parse(JsonSerializer.Serialize(body)).RootElement.GetProperty("message").GetString()!;

    // Confirmation: 400.
    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task InvalidStatusFilter_Returns400(int badStatus)
    {
        var r = await _sut.GetReceivedAsync(Guid.NewGuid(), badStatus);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Trang thai request contact khong hop le.", ErrorMessage(r.Body));
    }

    // Confirmation: 400.
    [Fact]
    public async Task NotNanny_Returns400()
    {
        var userId = Guid.NewGuid();
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId)).ReturnsAsync((NannyProfile?)null);

        var r = await _sut.GetReceivedAsync(userId, null);

        Assert.Equal(400, r.StatusCode);
        Assert.Equal("Tai khoan khong phai nanny.", ErrorMessage(r.Body));
        _mockCr.Verify(c => c.GetReceivedListForNannyAsync(It.IsAny<Guid>(), It.IsAny<int?>()), Times.Never);
    }

    // Confirmation: 200, data d?m 0, requests r?ng.
    [Fact]
    public async Task Success_EmptyList_Returns200()
    {
        var userId  = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetReceivedListForNannyAsync(nannyId, null))
            .ReturnsAsync((new List<ContactRequest>(), 0, 0, 0, 0));

        var r = await _sut.GetReceivedAsync(userId, null);

        Assert.Equal(200, r.StatusCode);
        var data = JsonDocument.Parse(JsonSerializer.Serialize(r.Body)).RootElement.GetProperty("data");
        Assert.Equal(0, data.GetProperty("totalRequests").GetInt32());
        Assert.Equal(0, data.GetProperty("pendingRequests").GetInt32());
        Assert.Equal(0, data.GetProperty("acceptedRequests").GetInt32());
        Assert.Equal(0, data.GetProperty("rejectedRequests").GetInt32());
        Assert.Equal(0, data.GetProperty("requests").GetArrayLength());
    }

    // Confirmation: map parent, canReview=true, g?i repo v?i (nannyId, 0).
    [Fact]
    public async Task Success_WithItem_MapsParent_Pending_CanReview()
    {
        var userId  = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        var ppId    = Guid.NewGuid();
        var pUser   = new User
        {
            Id = Guid.NewGuid(), FirstName = "Pa", LastName = "Rent", City = "HN", District = "Q1", AvatarUrl = "a.png", PhoneNumber = "09", Address = "1 st"
        };
        var parent  = new ParentProfile { Id = ppId, UserId = pUser.Id, User = pUser };
        var crId    = Guid.NewGuid();
        var at      = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var item = new ContactRequest
        {
            Id              = crId,
            ParentProfileId = ppId,
            NannyProfileId  = nannyId,
            Status          = 0,
            Message         = "hello",
            CreatedAt       = at,
            ParentProfile   = parent
        };

        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetReceivedListForNannyAsync(nannyId, 0))
            .ReturnsAsync(([item], 1, 1, 0, 0));

        var r = await _sut.GetReceivedAsync(userId, 0);

        Assert.Equal(200, r.StatusCode);
        _mockCr.Verify(c => c.GetReceivedListForNannyAsync(nannyId, 0), Times.Once);

        var first = JsonDocument.Parse(JsonSerializer.Serialize(r.Body))
            .RootElement
            .GetProperty("data")
            .GetProperty("requests")[0];
        Assert.Equal(crId, first.GetProperty("id").GetGuid());
        Assert.Equal(0, first.GetProperty("status").GetInt32());
        Assert.Equal("Dang cho duyet", first.GetProperty("statusLabel").GetString());
        Assert.Equal("hello", first.GetProperty("message").GetString());
        Assert.True(first.GetProperty("canReview").GetBoolean());
        var pr = first.GetProperty("parent");
        Assert.Equal(ppId, pr.GetProperty("profileId").GetGuid());
        Assert.Equal(pUser.Id, pr.GetProperty("userId").GetGuid());
        Assert.Equal("Pa Rent", pr.GetProperty("fullName").GetString());
        Assert.Equal("a.png", pr.GetProperty("avatarUrl").GetString());
    }

    // Confirmation: canReview=false.
    [Fact]
    public async Task Success_AcceptedItem_CanReviewFalse()
    {
        var userId  = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        var item = new ContactRequest
        {
            Id              = Guid.NewGuid(),
            ParentProfileId = Guid.NewGuid(),
            NannyProfileId  = nannyId,
            Status          = 1,
            CreatedAt       = DateTime.UtcNow,
            ParentProfile   = new ParentProfile
            {
                Id     = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                User   = new User { FirstName = "X", LastName = "Y" }
            }
        };
        _mockNanny.Setup(n => n.FindByUserIdAsync(userId))
            .ReturnsAsync(new NannyProfile { Id = nannyId, UserId = userId });
        _mockCr.Setup(c => c.GetReceivedListForNannyAsync(nannyId, null))
            .ReturnsAsync(([item], 1, 0, 1, 0));

        var r = await _sut.GetReceivedAsync(userId, null);

        var first = JsonDocument.Parse(JsonSerializer.Serialize(r.Body))
            .RootElement
            .GetProperty("data")
            .GetProperty("requests")[0];
        Assert.False(first.GetProperty("canReview").GetBoolean());
        Assert.Equal("Da duoc chap nhan", first.GetProperty("statusLabel").GetString());
    }
}
