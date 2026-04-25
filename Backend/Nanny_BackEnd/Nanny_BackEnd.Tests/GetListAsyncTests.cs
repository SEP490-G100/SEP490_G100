using Moq;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="NanniesController.GetNannies"/> ? <see cref="NannyService.GetListAsync"/>.
/// </summary>
public class GetListAsyncTests
{
    private readonly Mock<INannyProfileRepository> _mockNanny;
    private readonly Mock<IFavoriteRepository>   _mockFav;
    private readonly Mock<INotificationService>  _mockNotif;
    private readonly NannyService                 _sut;

    public GetListAsyncTests()
    {
        _mockNanny = new Mock<INannyProfileRepository>();
        _mockFav   = new Mock<IFavoriteRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut       = new NannyService(_mockNanny.Object, _mockFav.Object, _mockNotif.Object);
    }

    private static NannyProfile MakeProfile(Guid id, string first = "Mai", string last = "Lan")
    {
        return new NannyProfile
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
    }

    [Fact]
    public async Task ReturnsNanniesFromRepo()
    {
        var p = MakeProfile(Guid.NewGuid());
        _mockNanny.Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(([p], 1));

        var result = await _sut.GetListAsync(new NannyListRequest { Page = 1, PageSize = 12 }, null);

        var item = Assert.Single(result.Items);
        Assert.Equal("Mai Lan", item.FullName);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(12, result.PageSize);
        Assert.False(item.IsFavorite);
    }

    // Confirmation: IsFavorite = true.
    [Fact]
    public async Task WithParent_ResolvesIsFavorite()
    {
        var parentId = Guid.NewGuid();
        var nanny    = MakeProfile(Guid.NewGuid());
        _mockNanny.Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(([nanny], 1));
        _mockFav.Setup(f => f.getFavoriteNannyIds(parentId, It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new HashSet<Guid> { nanny.Id });

        var result = await _sut.GetListAsync(new NannyListRequest(), parentId);

        Assert.True(result.Items[0].IsFavorite);
    }

    [Fact]
    public async Task SkillIds_InvalidTokensIgnored()
    {
        var good = Guid.NewGuid();
        _mockNanny
            .Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.Is<IEnumerable<Guid>>(g => g!.SequenceEqual(new[] { good }))))
            .ReturnsAsync((new List<NannyProfile>(), 0));

        var result = await _sut.GetListAsync(
            new NannyListRequest { SkillIds = $"  {good}, not-a-guid, {Guid.Empty}  " },
            null);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
