using Moq;
using FluentAssertions;
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

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
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

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { mod1.Id, mod2.Id });
        result.Items.Select(i => i.Email).Should().BeEquivalentTo(new[] { mod1.Email, mod2.Email });
        // Role được map từ UserRoles
        result.Items.First(i => i.Id == mod1.Id).Roles.Should().Contain("Moderator");
    }

    // ── TC3: page < 1 → clamp về 1, repo được gọi với page = 1 ──────────
    [Fact]
    public async Task InvalidPage_ClampsToOne()
    {
        _mockRepo.Setup(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10))
                 .ReturnsAsync((new List<User>(), 0));

        var result = await _sut.AdminViewModeratorAccountListAsync(null, null, page: 0, pageSize: 10);

        result.Page.Should().Be(1);
        _mockRepo.Verify(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10), Times.Once);
    }

    // ── TC4: pageSize > 100 → clamp về 10, repo được gọi với pageSize = 10
    [Fact]
    public async Task InvalidPageSize_ClampsToDefault()
    {
        _mockRepo.Setup(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10))
                 .ReturnsAsync((new List<User>(), 0));

        var result = await _sut.AdminViewModeratorAccountListAsync(null, null, page: 1, pageSize: 200);

        result.PageSize.Should().Be(10);
        _mockRepo.Verify(r => r.GetPagedModeratorAccountsAsync(null, null, 1, 10), Times.Once);
    }
}
