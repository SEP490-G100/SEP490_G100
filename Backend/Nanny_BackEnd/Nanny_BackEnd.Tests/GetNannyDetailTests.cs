using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="NanniesController.GetNannyDetail"/> → <see cref="NannyService.GetDetailAsync"/>.
/// </summary>
public class GetNannyDetailTests
{
    private readonly Mock<INannyProfileRepository> _mockNanny;
    private readonly Mock<IFavoriteRepository>   _mockFav;
    private readonly Mock<INotificationService>  _mockNotif;
    private readonly NannyService                 _sut;

    public GetNannyDetailTests()
    {
        _mockNanny = new Mock<INannyProfileRepository>();
        _mockFav   = new Mock<IFavoriteRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut       = new NannyService(_mockNanny.Object, _mockFav.Object, _mockNotif.Object);
    }

    private static NannyProfile MakeProfile(Guid id, string first = "Mai", string last = "Lan") => new()
    {
        Id        = id,
        UserId    = Guid.NewGuid(),
        User = new User
        {
            Id        = Guid.NewGuid(),
            FirstName = first,
            LastName  = last,
            City      = "HN"
        },
        NannySkills         = new List<NannySkill>(),
        NannyAvailabilities = new List<NannyAvailability>()
    };

    // Condition: repo không có hồ sơ.
    // Confirmation: KeyNotFoundException với message thống nhất.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var id = Guid.NewGuid();
        _mockNanny.Setup(r => r.GetDetailAsync(id)).ReturnsAsync((NannyProfile?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetDetailAsync(id, null));

        Assert.Equal("Không tìm thấy hồ sơ bảo mẫu.", ex.Message);
        _mockFav.Verify(
            f => f.isFavoriteNanny(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never);
    }

    // Condition: có hồ sơ, không phụ huynh đăng nhập.
    // Confirmation: trả về detail, IsFavorite = false, không gọi isFavoriteNanny.
    [Fact]
    public async Task Success_NoParent_IsFavoriteFalse_NoFavoriteCall()
    {
        var p = MakeProfile(Guid.NewGuid());
        _mockNanny.Setup(r => r.GetDetailAsync(p.Id)).ReturnsAsync(p);

        var r = await _sut.GetDetailAsync(p.Id, null);

        Assert.Equal("Mai Lan", r.FullName);
        Assert.Equal(p.Id, r.Id);
        Assert.False(r.IsFavorite);
        _mockFav.Verify(
            f => f.isFavoriteNanny(It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never);
    }

    // Condition: phụ huynh đang xem, nanny nằm trong yêu thích.
    // Confirmation: IsFavorite = true, gọi isFavoriteNanny đúng 1 lần.
    [Fact]
    public async Task Success_WithParent_Favorite_ResolvesIsFavorite()
    {
        var parentId = Guid.NewGuid();
        var nanny    = MakeProfile(Guid.NewGuid());
        _mockNanny.Setup(r => r.GetDetailAsync(nanny.Id)).ReturnsAsync(nanny);
        _mockFav.Setup(f => f.isFavoriteNanny(parentId, nanny.Id)).ReturnsAsync(true);

        var r = await _sut.GetDetailAsync(nanny.Id, parentId);

        Assert.True(r.IsFavorite);
        _mockFav.Verify(f => f.isFavoriteNanny(parentId, nanny.Id), Times.Once);
    }

    // Condition: phụ huynh đang xem nhưng chưa yêu thích.
    // Confirmation: IsFavorite = false.
    [Fact]
    public async Task Success_WithParent_NotFavorite()
    {
        var parentId = Guid.NewGuid();
        var nanny    = MakeProfile(Guid.NewGuid());
        _mockNanny.Setup(r => r.GetDetailAsync(nanny.Id)).ReturnsAsync(nanny);
        _mockFav.Setup(f => f.isFavoriteNanny(parentId, nanny.Id)).ReturnsAsync(false);

        var r = await _sut.GetDetailAsync(nanny.Id, parentId);

        Assert.False(r.IsFavorite);
    }
}
