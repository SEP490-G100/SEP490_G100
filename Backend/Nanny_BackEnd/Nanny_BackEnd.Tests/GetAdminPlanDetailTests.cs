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
/// SubscriptionController.AdminViewSubscriptionPlanDetail -> SubscriptionService.getAdminPlanDetail
/// </summary>
public class GetAdminPlanDetailTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly SubscriptionService _sut;

    public GetAdminPlanDetailTests()
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

    private static string FeaturesJson() => JsonSerializer.Serialize(new List<string> { "F1" });

    // Condition: plan not found.
    [Fact]
    public async Task NotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync((SubscriptionPlan?)null);

        var r = await _sut.getAdminPlanDetail(id);

        Assert.Null(r);
        _mockRepo.Verify(
            r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    // Condition: plan found -> map detail and count active subscribers.
    [Fact]
    public async Task Found_ReturnsDetail()
    {
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "Parent Pro Test",
            Description = "D",
            Features = FeaturesJson(),
            Price = 50_000m,
            DurationDays = 60,
            SortOrder = 2,
            IsActive = true,
            CanUseRecommendation = true,
            IsDeleted = false,
            CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(id, It.IsAny<DateTime>()))
            .ReturnsAsync(4);

        var r = await _sut.getAdminPlanDetail(id);

        Assert.NotNull(r);
        Assert.Equal(id, r!.Id);
        Assert.Equal("Parent Pro Test", r.Name);
        Assert.Equal("D", r.Description);
        Assert.Equal(50_000m, r.Price);
        Assert.Equal(60, r.DurationDays);
        Assert.Equal(2, r.SortOrder);
        Assert.Equal("PARENT_PRO_TEST", r.Code);
        Assert.Equal("Parent", r.TargetRole);
        Assert.True(r.IsActive);
        Assert.Equal(4, r.ActiveSubscriberCount);
        Assert.True(r.CanUseRecommendation);
        Assert.Equal(plan.CreatedAt, r.CreatedAt);
        Assert.Equal(plan.UpdatedAt, r.UpdatedAt);
        Assert.NotEmpty(r.Features);
        _mockRepo.Verify(
            x => x.countActiveSubscriptionsByPlan(id, It.IsAny<DateTime>()),
            Times.Once);
    }
}
