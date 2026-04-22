using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.SubscriptionController.CancelCurrent"/> → <see cref="SubscriptionService.cancelCurrentSubscription"/>.
/// </summary>
public class CancelCurrentSubscriptionTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<ICassoService>            _mockCasso;
    private readonly Mock<IPayOsService>            _mockPayOs;
    private readonly SubscriptionService            _sut;

    public CancelCurrentSubscriptionTests()
    {
        _mockRepo  = new Mock<ISubscriptionRepository>();
        _mockNotif = new Mock<INotificationService>();
        _mockCasso = new Mock<ICassoService>();
        _mockPayOs = new Mock<IPayOsService>();
        var payOpt = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });
        _sut = new SubscriptionService(
            _mockRepo.Object,
            _mockNotif.Object,
            _mockCasso.Object,
            _mockPayOs.Object,
            payOpt);
    }

    private void StubExpireNoOp(Guid userId) =>
        _mockRepo.Setup(r => r.getExpiredActiveSubscriptions(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new List<UserSubscription>());

    private static SubscriptionPlan Plan(Guid id) => new()
    {
        Id            = id,
        Name          = "Goi phu huynh huy",
        Description   = "phu huynh; bai dang",
        Features      = "parent",
        Price         = 1,
        DurationDays  = 30,
        IsActive      = true,
        IsDeleted     = false,
        CreatedAt     = DateTime.UtcNow
    };

    // Condition: không có gói đang dùng.
    [Fact]
    public async Task NoActive_Throws()
    {
        var userId = Guid.NewGuid();
        StubExpireNoOp(userId);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync((UserSubscription?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.cancelCurrentSubscription(userId));
        Assert.Equal("Bạn không có gói subscription đang hoạt động.", ex.Message);
    }

    // Condition: hủy gói active → cập nhật trạng thái, lưu, map.
    [Fact]
    public async Task Success_SetsCancelled_Saves_Maps()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var pl     = Plan(planId);
        var end    = DateTime.UtcNow.AddDays(20);
        var sub = new UserSubscription
        {
            Id                 = Guid.NewGuid(),
            UserId             = userId,
            SubscriptionPlanId = planId,
            StartDate          = DateTime.UtcNow.AddDays(-10),
            EndDate            = end,
            Status             = (int)UserSubscriptionStatus.Active,
            IsDeleted          = false,
            CreatedAt          = DateTime.UtcNow,
            SubscriptionPlan   = pl
        };
        StubExpireNoOp(userId);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(sub);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);

        var r = await _sut.cancelCurrentSubscription(userId);

        Assert.Equal((int)UserSubscriptionStatus.Cancelled, sub.Status);
        Assert.Equal((int)UserSubscriptionStatus.Cancelled, r.Status);
        Assert.Equal("Đã hủy", r.StatusLabel);
        Assert.NotNull(sub.CancelledAt);
        Assert.Equal(userId, sub.UpdatedBy);
        _mockRepo.Verify(x => x.saveChanges(), Times.Once);
    }
}
