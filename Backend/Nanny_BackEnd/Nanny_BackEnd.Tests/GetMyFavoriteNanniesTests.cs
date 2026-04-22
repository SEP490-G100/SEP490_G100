using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="NanniesController.GetMyFavoriteNannies"/> → <see cref="NannyService.GetFavoritesAsync"/>.
/// </summary>
public class GetMyFavoriteNanniesTests
{
    private readonly Mock<INannyProfileRepository> _mockNanny;
    private readonly Mock<IFavoriteRepository>   _mockFav;
    private readonly Mock<INotificationService>  _mockNotif;
    private readonly NannyService                 _sut;

    public GetMyFavoriteNanniesTests()
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

    // Condition: 2 hồ sơ yêu thích, page=1, pageSize=12.
    // Confirmation: tất cả item IsFavorite = true, TotalCount/Page/PageSize đúng.
    [Fact]
    public async Task Normal_ReturnsFavorites_AllMarkedFavorite()
    {
        var parentId = Guid.NewGuid();
        var a = MakeProfile(Guid.NewGuid(), "A", "One");
        var b = MakeProfile(Guid.NewGuid(), "B", "Two");
        _mockFav.Setup(f => f.getFavoriteNannies(parentId, 1, 12))
            .ReturnsAsync(([a, b], 2));

        var result = await _sut.GetFavoritesAsync(parentId, 1, 12);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(12, result.PageSize);
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.Items[0].IsFavorite);
        Assert.True(result.Items[1].IsFavorite);
        Assert.Equal("A One", result.Items[0].FullName);
    }

    // Condition: Page=0, PageSize=0.
    // Confirmation: gọi repo với page=1, pageSize=12.
    [Fact]
    public async Task Boundary_LowPageAndPageSize_Clamped()
    {
        var parentId = Guid.NewGuid();
        _mockFav.Setup(f => f.getFavoriteNannies(parentId, 1, 12))
            .ReturnsAsync((new List<NannyProfile>(), 0));

        var result = await _sut.GetFavoritesAsync(parentId, page: 0, pageSize: 0);

        Assert.Equal(1, result.Page);
        Assert.Equal(12, result.PageSize);
        _mockFav.Verify(f => f.getFavoriteNannies(parentId, 1, 12), Times.Once);
    }

    // Condition: PageSize vượt 50.
    // Confirmation: PageSize trả về 50, repo nhận 50.
    [Fact]
    public async Task Boundary_HighPageSize_CappedAt50()
    {
        var parentId = Guid.NewGuid();
        _mockFav.Setup(f => f.getFavoriteNannies(parentId, 1, 50))
            .ReturnsAsync((new List<NannyProfile>(), 0));

        var result = await _sut.GetFavoritesAsync(parentId, 1, 200);

        Assert.Equal(50, result.PageSize);
        _mockFav.Verify(f => f.getFavoriteNannies(parentId, 1, 50), Times.Once);
    }

    // Condition: không có bản ghi nào.
    // Confirmation: Items rỗng, TotalCount = 0.
    [Fact]
    public async Task Boundary_EmptyResult()
    {
        var parentId = Guid.NewGuid();
        _mockFav.Setup(f => f.getFavoriteNannies(parentId, 1, 12))
            .ReturnsAsync((new List<NannyProfile>(), 0));

        var result = await _sut.GetFavoritesAsync(parentId);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
