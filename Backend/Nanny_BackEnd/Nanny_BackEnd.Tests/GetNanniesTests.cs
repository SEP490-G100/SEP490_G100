using Moq;
using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="NanniesController.GetNannies"/> → <see cref="NannyService.GetListAsync"/>.
/// Condition = precondition + input; Confirmation = kết quả hàm.
/// </summary>
public class GetNanniesTests
{
    private readonly Mock<INannyProfileRepository> _mockNanny;
    private readonly Mock<IFavoriteRepository>   _mockFav;
    private readonly Mock<INotificationService>  _mockNotif;
    private readonly NannyService                 _sut;

    public GetNanniesTests()
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

    // ── Normal ───────────────────────────────────────────────────────
    // Condition: search trả 1 hồ sơ; optional parent = null.
    // Confirmation: Items/TotalCount/Page/PageSize, không gọi favorite.
    [Fact]
    public async Task Normal_ReturnsList_NoParent_NoFavoriteCall()
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
        _mockFav.Verify(
            f => f.getFavoriteNannyIds(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()),
            Times.Never);
    }

    // Condition: có currentParentProfileId, nanny nằm trong tập yêu thích.
    // Confirmation: IsFavorite = true, gọi getFavoriteNannyIds đúng 1 lần.
    [Fact]
    public async Task Normal_Parent_ResolvesIsFavorite()
    {
        var parentId = Guid.NewGuid();
        var nanny    = MakeProfile(Guid.NewGuid());
        _mockNanny.Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(([nanny], 1));
        _mockFav.Setup(f => f.getFavoriteNannyIds(parentId, It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new HashSet<Guid> { nanny.Id });

        var result = await _sut.GetListAsync(new NannyListRequest(), parentId);

        Assert.True(result.Items[0].IsFavorite);
        _mockFav.Verify(f => f.getFavoriteNannyIds(parentId, It.IsAny<IEnumerable<Guid>>()), Times.Once);
    }

    // ── Boundary (clamp page / pageSize) ─────────────────────────────
    // Condition: Page=0, PageSize=0.
    // Confirmation: response dùng Page=1, PageSize=12 (cận dưới).
    [Fact]
    public async Task Boundary_LowPageAndPageSize_Clamped()
    {
        _mockNanny.Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((new List<NannyProfile>(), 0));

        var result = await _sut.GetListAsync(new NannyListRequest { Page = 0, PageSize = 0 }, null);

        Assert.Equal(1, result.Page);
        Assert.Equal(12, result.PageSize);
    }

    // Condition: PageSize vượt trần 50.
    // Confirmation: PageSize trả về 50.
    [Fact]
    public async Task Boundary_HighPageSize_CappedAt50()
    {
        _mockNanny.Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((new List<NannyProfile>(), 0));

        var result = await _sut.GetListAsync(new NannyListRequest { Page = 1, PageSize = 200 }, null);

        Assert.Equal(50, result.PageSize);
    }

    // Condition: repo trả 0 bản ghi.
    // Confirmation: Items rỗng, TotalCount=0.
    [Fact]
    public async Task Boundary_EmptyResult_ReturnsEmptyItems()
    {
        _mockNanny.Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((new List<NannyProfile>(), 0));

        var result = await _sut.GetListAsync(new NannyListRequest(), null);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ── Abnormal / tập input không hợp lệ (SkillIds) ────────────────
    // Condition: SkillIds chứa chuỗi không phải GUID; chỉ guid hợp lệ được parse.
    // Confirmation: SearchAsync nhận danh sách skill đã lọc (bỏ rác).
    [Fact]
    public async Task Abnormal_SkillIds_IgnoresInvalidGuidTokens()
    {
        var good = Guid.NewGuid();
        _mockNanny
            .Setup(r => r.SearchAsync(It.IsAny<NannyListRequest>(), It.Is<IEnumerable<Guid>>(g => g!.SequenceEqual(new[] { good }))))
            .ReturnsAsync((new List<NannyProfile>(), 0))
            .Verifiable();

        await _sut.GetListAsync(
            new NannyListRequest { SkillIds = $"  {good}, not-a-guid, {Guid.Empty}  " },
            null);

        _mockNanny.Verify();
    }
}
