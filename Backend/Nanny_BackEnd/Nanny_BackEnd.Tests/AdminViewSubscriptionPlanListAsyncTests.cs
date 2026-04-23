using System.Text.Json;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.AdminSubcriptionPlanController.AdminViewSubscriptionPlanList"/> →
/// <see cref="AdminSubcriptionPlanService.AdminViewSubscriptionPlanListAsync"/>.
/// </summary>
public class AdminViewSubscriptionPlanListAsyncTests
{
    private readonly Mock<IAdminSubcriptionPlanRepository> _mockRepo;
    private readonly AdminSubcriptionPlanService _sut;

    public AdminViewSubscriptionPlanListAsyncTests()
    {
        _mockRepo = new Mock<IAdminSubcriptionPlanRepository>();
        _sut = new AdminSubcriptionPlanService(_mockRepo.Object);
    }

    private static string FeaturesJson(string code, string targetRole) =>
        JsonSerializer.Serialize(new SubscriptionPlanMetadata
        {
            Code = code,
            TargetRole = targetRole,
            Features = new List<string> { "feature" },
            Benefits = new SubscriptionBenefitResponse
            {
                MonthlyJobPostLimit = 1,
                MonthlyApplicationLimit = 0,
                FeaturedBadge = false,
                SearchPriority = false,
                ListingDurationDays = 30,
                CanUseRecommendation = false
            }
        });

    private static SubscriptionPlan Plan(
        Guid id,
        string name,
        string code,
        string targetRole,
        int sort,
        decimal price = 10_000m,
        bool isActive = true) => new()
    {
        Id = id,
        Name = name,
        Features = FeaturesJson(code, targetRole),
        Price = price,
        DurationDays = 30,
        SortOrder = sort,
        IsActive = isActive,
        CanUseRecommendation = false,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow
    };

    // Condition: repository trả về rỗng.
    [Fact]
    public async Task NoPlans_EmptyList_TotalPagesOne()
    {
        _mockRepo.Setup(r => r.GetAdminPlansIncludingDeletedAsync()).ReturnsAsync(new List<SubscriptionPlan>());
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.AdminViewSubscriptionPlanListAsync(null, null, null, 1, 10);

        Assert.Empty(r.Items);
        Assert.Equal(0, r.TotalCount);
        Assert.Equal(1, r.TotalPages);
    }

    // Condition: tìm theo tên (không phân biệt hoa thường).
    [Fact]
    public async Task Search_FiltersByName()
    {
        var a = Plan(Guid.NewGuid(), "Gói Plus", "P1", "Parent", 1, 5_000m);
        var b = Plan(Guid.NewGuid(), "Gói Basic", "B1", "Parent", 2, 3_000m);
        _mockRepo.Setup(x => x.GetAdminPlansIncludingDeletedAsync())
            .ReturnsAsync(new List<SubscriptionPlan> { a, b });
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.AdminViewSubscriptionPlanListAsync("  plus  ", null, null, 1, 10);

        Assert.Equal(1, r.TotalCount);
        Assert.Equal("Gói Plus", r.Items[0].Name);
    }

    // Condition: lọc theo isActive.
    [Fact]
    public async Task Filter_IsActive()
    {
        var on = Plan(Guid.NewGuid(), "A", "C1", "Nanny", 1, 1_000m, isActive: true);
        var off = Plan(Guid.NewGuid(), "B", "C2", "Nanny", 2, 2_000m, isActive: false);
        _mockRepo.Setup(x => x.GetAdminPlansIncludingDeletedAsync())
            .ReturnsAsync(new List<SubscriptionPlan> { on, off });
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.AdminViewSubscriptionPlanListAsync(null, null, true, 1, 10);

        Assert.Equal(1, r.TotalCount);
        Assert.True(r.Items[0].IsActive);
    }

    // Condition: lọc theo TargetRole.
    [Fact]
    public async Task Filter_NormalizesTargetRole()
    {
        var parent = Plan(Guid.NewGuid(), "P", "PC", "Parent", 1);
        var nanny = Plan(Guid.NewGuid(), "N", "NC", "Nanny", 2);
        _mockRepo.Setup(x => x.GetAdminPlansIncludingDeletedAsync())
            .ReturnsAsync(new List<SubscriptionPlan> { parent, nanny });
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.AdminViewSubscriptionPlanListAsync(null, "nanny", null, 1, 10);

        Assert.Equal(1, r.TotalCount);
        Assert.Equal("Nanny", r.Items[0].TargetRole);
    }

    // Condition: phân trang; page / pageSize dưới 1 được nâng lên 1; gọi CountActive cho từng bản ghi trang.
    [Fact]
    public async Task Paging_AndSubCounts()
    {
        var p1 = Plan(Guid.NewGuid(), "A", "C1", "Parent", 1, 1_000m);
        var p2 = Plan(Guid.NewGuid(), "B", "C2", "Parent", 2, 2_000m);
        _mockRepo.Setup(x => x.GetAdminPlansIncludingDeletedAsync())
            .ReturnsAsync(new List<SubscriptionPlan> { p1, p2 });
        _mockRepo.Setup(r => r.CountActiveSubscriptionsByPlanAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(5);

        var r = await _sut.AdminViewSubscriptionPlanListAsync(null, null, null, 0, 0);

        Assert.Equal(1, r.Page);
        Assert.Equal(1, r.PageSize);
        Assert.Single(r.Items);
        Assert.Equal(2, r.TotalCount);
        Assert.Equal(2, r.TotalPages);
        Assert.Equal(5, r.Items[0].ActiveSubscriberCount);
        _mockRepo.Verify(
            x => x.CountActiveSubscriptionsByPlanAsync(r.Items[0].Id, It.IsAny<DateTime>()),
            Times.Once);
    }
}
