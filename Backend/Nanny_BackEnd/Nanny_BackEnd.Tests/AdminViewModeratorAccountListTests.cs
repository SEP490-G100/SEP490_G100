using System.Linq;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class AdminViewModeratorAccountListTests
{
    private readonly Mock<IAdminAccountRepository> _mockRepo;
    private readonly AdminAccountService          _sut;

    public AdminViewModeratorAccountListTests()
    {
        _mockRepo = new Mock<IAdminAccountRepository>();
        _sut      = new AdminAccountService(_mockRepo.Object);
    }

    // ── Helper: tạo User có role Moderator ───────────────────────────────
    private static User MakeModerator(string email = "mod@test.com") => new()
    {
        Id        = Guid.NewGuid(),
        Email     = email,
        FirstName = "Mod",
        LastName  = "One",
        Status    = 1,
        CreatedAt = DateTime.UtcNow,
        UserRoles = new List<UserRole>
        {
            new() { IsDeleted = false, Role = new Role { Name = "Moderator" } }
        }
    };

    // ── TC1: Không có moderator nào → Items rỗng, TotalCount = 0 ─────────
    [Fact]
    public async Task EmptyList()
    {
        _mockRepo.Setup(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10))
                 .ReturnsAsync((new List<User>(), 0));

        var result = await _sut.AdminViewModeratorAccountListAsync(null, null, 1, 10);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    // ── TC2: Có 2 moderator → Items đủ số, mapping Id và Email đúng ──────
    [Fact]
    public async Task WithResults_MapsCorrectly()
    {
        var mod1 = MakeModerator("mod1@test.com");
        var mod2 = MakeModerator("mod2@test.com");

        _mockRepo.Setup(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10))
                 .ReturnsAsync((new List<User> { mod1, mod2 }, 2));

        var result = await _sut.AdminViewModeratorAccountListAsync(null, null, 1, 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(
            new[] { mod1.Id, mod2.Id }.OrderBy(x => x),
            result.Items.Select(i => i.Id).OrderBy(x => x));
        Assert.Equal(
            new[] { mod1.Email, mod2.Email }.OrderBy(x => x),
            result.Items.Select(i => i.Email).OrderBy(x => x));
        // Role được map từ UserRoles
        Assert.Contains("Moderator", result.Items.First(i => i.Id == mod1.Id).Roles);
    }

    // ── TC3: page < 1 → clamp về 1, repo được gọi với page = 1 ──────────
    [Fact]
    public async Task InvalidPage_ClampsToOne()
    {
        _mockRepo.Setup(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10))
                 .ReturnsAsync((new List<User>(), 0));

        var result = await _sut.AdminViewModeratorAccountListAsync(null, null, page: 0, pageSize: 10);

        Assert.Equal(1, result.Page);
        _mockRepo.Verify(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10), Times.Once);
    }

    // ── TC4: pageSize > 100 → clamp về 10, repo được gọi với pageSize = 10
    [Fact]
    public async Task InvalidPageSize_ClampsToDefault()
    {
        _mockRepo.Setup(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10))
                 .ReturnsAsync((new List<User>(), 0));

        var result = await _sut.AdminViewModeratorAccountListAsync(null, null, page: 1, pageSize: 200);

        Assert.Equal(10, result.PageSize);
        _mockRepo.Verify(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10), Times.Once);
    }
}
