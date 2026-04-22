using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Trạng thái gói subscription (admin) →
/// <see cref="AdminSubcriptionPlanService.AdminUpdateSubscriptionPlanStatusAsync"/>.
/// </summary>
public class AdminUpdateSubscriptionPlanStatusAsyncTests
{
    private readonly Mock<IAdminSubcriptionPlanRepository> _mockRepo;
    private readonly AdminSubcriptionPlanService _sut;

    public AdminUpdateSubscriptionPlanStatusAsyncTests()
    {
        _mockRepo = new Mock<IAdminSubcriptionPlanRepository>();
        _sut = new AdminSubcriptionPlanService(_mockRepo.Object);
    }

    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync((SubscriptionPlan?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.AdminUpdateSubscriptionPlanStatusAsync(id, Guid.NewGuid(), isActive: true));

        Assert.Equal("Khong tim thay goi subscription.", ex.Message);
    }

    // Condition: đã đúng (đang bật, không bị xóa mềm) — không ghi DB.
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
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);

        await _sut.AdminUpdateSubscriptionPlanStatusAsync(id, Guid.NewGuid(), isActive: true);

        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // Condition: đã tắt + IsDeleted theo nghĩa “ẩn” — không ghi DB.
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
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);

        await _sut.AdminUpdateSubscriptionPlanStatusAsync(id, Guid.NewGuid(), isActive: false);

        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // Condition: tắt gói — IsActive false, IsDeleted true, audit.
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
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        var before = DateTime.UtcNow;

        await _sut.AdminUpdateSubscriptionPlanStatusAsync(id, adminId, isActive: false);

        Assert.False(plan.IsActive);
        Assert.True(plan.IsDeleted);
        Assert.Equal(adminId, plan.UpdatedBy);
        Assert.NotNull(plan.UpdatedAt);
        Assert.True(plan.UpdatedAt >= before);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // Condition: bật lại — IsActive true, IsDeleted false.
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
        _mockRepo.Setup(r => r.FindAdminPlanByIdIncludingDeletedAsync(id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        var before = DateTime.UtcNow;

        await _sut.AdminUpdateSubscriptionPlanStatusAsync(id, adminId, isActive: true);

        Assert.True(plan.IsActive);
        Assert.False(plan.IsDeleted);
        Assert.Equal(adminId, plan.UpdatedBy);
        Assert.NotNull(plan.UpdatedAt);
        Assert.True(plan.UpdatedAt >= before);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
