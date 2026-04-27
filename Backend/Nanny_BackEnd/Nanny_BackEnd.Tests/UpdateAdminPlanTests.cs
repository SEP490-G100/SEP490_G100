using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Tests for:
/// SubscriptionController.AdminUpdateSubscriptionPlan -> SubscriptionService.updateAdminPlan
/// </summary>
public class AdminUpdateSubscriptionPlanAsyncTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly SubscriptionService _sut;

    public AdminUpdateSubscriptionPlanAsyncTests()
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

    private static string FeaturesJson() => JsonSerializer.Serialize(new List<string> { "old" });

    private static AdminSubscriptionPlanUpsertRequest ValidRequest() => new()
    {
        Name = "Nanny Premium",
        Description = "Mo ta cap nhat",
        TargetRole = "Nanny",
        Price = 120_000m,
        DurationDays = 60,
        SortOrder = 7,
        Features = new List<string> { "Nang 1" },
        Benefits = new AdminSubscriptionPlanBenefitRequest
        {
            MonthlyJobPostLimit = 0,
            MonthlyApplicationLimit = 10,
            FeaturedBadge = false,
            SearchPriority = true,
            ListingDurationDays = 0
        },
        CanUseRecommendation = false
    };

    // Condition: validation before loading plan.
    [Fact]
    public async Task ValidationFails_Throws_DoesNotQueryPlan()
    {
        var req = ValidRequest();
        req.Features = new List<string> { "   " };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.updateAdminPlan(Guid.NewGuid(), Guid.NewGuid(), req));

        Assert.Contains("feature", ex.Message, StringComparison.OrdinalIgnoreCase);
        _mockRepo.Verify(r => r.findAdminPlanByIdIncludingDeleted(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: not found by id.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync((SubscriptionPlan?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.updateAdminPlan(id, Guid.NewGuid(), ValidRequest()));

    }

    // Condition: update success; uses request.SortOrder and does not call create-only methods.
    [Fact]
    public async Task Update_Success_MutatesPlan_ReturnsDetail()
    {
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var createdAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "Tên cũ",
            Description = "Mô tả cũ",
            Features = FeaturesJson(),
            Price = 10_000m,
            DurationDays = 7,
            SortOrder = 1,
            IsActive = true,
            IsDeleted = false,
            CanUseRecommendation = true,
            CreatedAt = createdAt
        };

        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(id, It.IsAny<DateTime>())).ReturnsAsync(3);

        var req = ValidRequest();
        req.Name = "  Nanny Pro  ";
        req.Description = "  mới  ";
        req.SortOrder = 7;
        req.Features = new List<string> { "Nang 1", "Nang 1", "Nang 2" };

        var r = await _sut.updateAdminPlan(id, adminId, req);

        Assert.Equal("Nanny Pro", plan.Name);
        Assert.Equal("mới", plan.Description);
        Assert.Equal(120_000m, plan.Price);
        Assert.Equal(60, plan.DurationDays);
        Assert.Equal(7, plan.SortOrder);
        Assert.False(plan.CanUseRecommendation);
        Assert.Equal(adminId, plan.UpdatedBy);
        Assert.NotNull(plan.UpdatedAt);

        var meta = SubscriptionPlanMetadataHelper.TryParse(plan.Features);
        Assert.NotNull(meta);
        Assert.Equal(2, meta!.Features.Count);
        Assert.Contains("Nang 1", meta.Features);
        Assert.Contains("Nang 2", meta.Features);

        Assert.Equal(id, r.Id);
        Assert.Equal("Nanny Pro", r.Name);
        Assert.Equal(7, r.SortOrder);
        Assert.Equal(120_000m, r.Price);
        Assert.Equal(3, r.ActiveSubscriberCount);
        Assert.Equal(createdAt, r.CreatedAt);
        Assert.Equal("Nanny", r.TargetRole);

        _mockRepo.Verify(r => r.saveChanges(), Times.Once);
        _mockRepo.Verify(r => r.getNextSubscriptionPlanSortOrder(), Times.Never);
        _mockRepo.Verify(r => r.addPlan(It.IsAny<SubscriptionPlan>()), Times.Never);
        _mockRepo.Verify(x => x.countActiveSubscriptionsByPlan(id, It.IsAny<DateTime>()), Times.Once);
    }

    // Condition: empty description is stored as null.
    [Fact]
    public async Task Update_EmptyDescription_StoresNull()
    {
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            Name = "Nanny A",
            Features = FeaturesJson()
        };
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.countActiveSubscriptionsByPlan(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var req = ValidRequest();
        req.Name = "Nanny A";
        req.Description = "   ";

        var r = await _sut.updateAdminPlan(id, Guid.NewGuid(), req);

        Assert.Null(plan.Description);
        Assert.Null(r.Description);
    }
}
