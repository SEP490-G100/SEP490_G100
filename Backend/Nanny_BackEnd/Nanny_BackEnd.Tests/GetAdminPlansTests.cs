using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Tests for:
/// SubscriptionController.AdminViewSubscriptionPlanList -> SubscriptionService.getAdminPlans
/// </summary>
public class GetAdminPlansTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly SubscriptionService _sut;

    public GetAdminPlansTests()
    {
        _mockRepo = new Mock<ISubscriptionRepository>();

        var mockNotificationService = new Mock<INotificationService>();
        var mockCassoService = new Mock<ICassoService>();
        var mockPayOsService = new Mock<IPayOsService>();
        var mockUserRepository = new Mock<IUserRepository>();
        var payOsOptions = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });

        _sut = new SubscriptionService(
            _mockRepo.Object,
            mockUserRepository.Object,
            mockNotificationService.Object,
            mockCassoService.Object,
            mockPayOsService.Object,
            payOsOptions,
            NullLogger<SubscriptionService>.Instance);
    }

    private static string FeaturesJson() => JsonSerializer.Serialize(new List<string> { "feature" });

    private static SubscriptionPlan Plan(
        Guid id,
        string name,
        int sort,
        decimal price = 10_000m,
        bool isActive = true) => new()
    {
        Id = id,
        Name = name,
        Features = FeaturesJson(),
        Price = price,
        DurationDays = 30,
        SortOrder = sort,
        IsActive = isActive,
        CanUseRecommendation = false,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow
    };

    // Condition: repository returns empty.
    [Fact]
    public async Task NoPlans_EmptyList_TotalPagesOne()
    {
        _mockRepo.Setup(r => r.getAdminPlansIncludingDeleted()).ReturnsAsync(new List<SubscriptionPlan>());

        var r = await _sut.getAdminPlans(null, null, null, 1, 10);

        Assert.Empty(r.Items);
        Assert.Equal(0, r.TotalCount);
        Assert.Equal(1, r.TotalPages);
    }

    // Condition: search by name (case-insensitive).
    [Fact]
    public async Task Search_FiltersByName()
    {
        var a = Plan(Guid.NewGuid(), "Parent Plus", 1, 5_000m);
        var b = Plan(Guid.NewGuid(), "Parent Basic", 2, 3_000m);
        _mockRepo.Setup(x => x.getAdminPlansIncludingDeleted())
            .ReturnsAsync(new List<SubscriptionPlan> { a, b });
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.getAdminPlans("  plus  ", null, null, 1, 10);

        Assert.Equal(1, r.TotalCount);
        Assert.Equal("Parent Plus", r.Items[0].Name);
    }

    // Condition: filter by isActive.
    [Fact]
    public async Task Filter_IsActive()
    {
        var on = Plan(Guid.NewGuid(), "Nanny A", 1, 1_000m, isActive: true);
        var off = Plan(Guid.NewGuid(), "Nanny B", 2, 2_000m, isActive: false);
        _mockRepo.Setup(x => x.getAdminPlansIncludingDeleted())
            .ReturnsAsync(new List<SubscriptionPlan> { on, off });
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.getAdminPlans(null, null, true, 1, 10);

        Assert.Equal(1, r.TotalCount);
        Assert.True(r.Items[0].IsActive);
    }

    // Condition: filter by target role.
    [Fact]
    public async Task Filter_NormalizesTargetRole()
    {
        var parent = Plan(Guid.NewGuid(), "Parent Plan", 1);
        var nanny = Plan(Guid.NewGuid(), "Nanny Plan", 2);
        _mockRepo.Setup(x => x.getAdminPlansIncludingDeleted())
            .ReturnsAsync(new List<SubscriptionPlan> { parent, nanny });
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var r = await _sut.getAdminPlans(null, "nanny", null, 1, 10);

        Assert.Equal(1, r.TotalCount);
        Assert.Equal("Nanny", r.Items[0].TargetRole);
    }

    // Condition: sub-counts được tính đúng cho từng plan.
    [Fact]
    public async Task SubCountsPerPlan()
    {
        var p1 = Plan(Guid.NewGuid(), "Parent A", 1, 1_000m);
        var p2 = Plan(Guid.NewGuid(), "Parent B", 2, 2_000m);
        _mockRepo.Setup(x => x.getAdminPlansIncludingDeleted())
            .ReturnsAsync(new List<SubscriptionPlan> { p1, p2 });
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(5);

        var r = await _sut.getAdminPlans(null, null, null, 1, 10);

        Assert.Equal(2, r.TotalCount);
        Assert.All(r.Items, item => Assert.Equal(5, item.ActiveSubscriberCount));
    }
}
