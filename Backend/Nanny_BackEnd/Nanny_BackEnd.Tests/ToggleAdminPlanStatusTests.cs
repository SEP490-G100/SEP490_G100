using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Tests for:
/// SubscriptionController.AdminUpdateSubscriptionPlanStatus -> SubscriptionService.toggleAdminPlanStatus
/// </summary>
public class AdminUpdateSubscriptionPlanStatusAsyncTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly SubscriptionService _sut;

    public AdminUpdateSubscriptionPlanStatusAsyncTests()
    {
        _mockRepo = new Mock<ISubscriptionRepository>();

        var mockNotificationService = new Mock<INotificationService>();
        var mockCassoService = new Mock<ICassoService>();
        var mockPayOsService = new Mock<IPayOsService>();
        var payOsOptions = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });

        _sut = new SubscriptionService(
            _mockRepo.Object,
            mockNotificationService.Object,
            mockCassoService.Object,
            mockPayOsService.Object,
            payOsOptions);
    }

    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync((SubscriptionPlan?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.toggleAdminPlanStatus(id, Guid.NewGuid(), isActive: true));

    }

    // Condition: already active and not deleted -> skip save.
    [Fact]
    public async Task AlreadyActive_SkipIsDeletedFalse_NoSave()
    {
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            IsActive = true,
            IsDeleted = false
        };
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync(plan);

        await _sut.toggleAdminPlanStatus(id, Guid.NewGuid(), isActive: true);

        _mockRepo.Verify(r => r.saveChanges(), Times.Never);
    }

    // Condition: already inactive and deleted -> skip save.
    [Fact]
    public async Task AlreadyInactive_SkipIsDeletedTrue_NoSave()
    {
        var id = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            IsActive = false,
            IsDeleted = true
        };
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync(plan);

        await _sut.toggleAdminPlanStatus(id, Guid.NewGuid(), isActive: false);

        _mockRepo.Verify(r => r.saveChanges(), Times.Never);
    }

    // Condition: deactivate plan.
    [Fact]
    public async Task Deactivate_Persists()
    {
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            IsActive = true,
            IsDeleted = false,
            UpdatedAt = null
        };
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        var before = DateTime.UtcNow;

        await _sut.toggleAdminPlanStatus(id, adminId, isActive: false);

        Assert.False(plan.IsActive);
        Assert.True(plan.IsDeleted);
        Assert.Equal(adminId, plan.UpdatedBy);
        Assert.NotNull(plan.UpdatedAt);
        Assert.True(plan.UpdatedAt >= before);
        _mockRepo.Verify(r => r.saveChanges(), Times.Once);
    }

    // Condition: reactivate plan.
    [Fact]
    public async Task Reactivate_Persists()
    {
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var plan = new SubscriptionPlan
        {
            Id = id,
            IsActive = false,
            IsDeleted = true,
            UpdatedAt = null
        };
        _mockRepo.Setup(r => r.findAdminPlanByIdIncludingDeleted(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        var before = DateTime.UtcNow;

        await _sut.toggleAdminPlanStatus(id, adminId, isActive: true);

        Assert.True(plan.IsActive);
        Assert.False(plan.IsDeleted);
        Assert.Equal(adminId, plan.UpdatedBy);
        Assert.NotNull(plan.UpdatedAt);
        Assert.True(plan.UpdatedAt >= before);
        _mockRepo.Verify(r => r.saveChanges(), Times.Once);
    }
}
