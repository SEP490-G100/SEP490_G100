using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.SubscriptionController.GetCurrentSubscription"/> → <see cref="SubscriptionService.getCurrentSubscription"/>.
/// </summary>
public class GetCurrentSubscriptionTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<ICassoService>          _mockCasso;
    private readonly Mock<IPayOsService>          _mockPayOs;
    private readonly SubscriptionService          _sut;

    public GetCurrentSubscriptionTests()
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
        Name          = "Goi phu huynh current",
        Description   = "phu huynh; bai dang",
        Features      = "parent",
        Price         = 100000m,
        DurationDays  = 30,
        IsActive      = true,
        IsDeleted     = false,
        CreatedAt     = DateTime.UtcNow
    };

    // Condition: hết hạn cũ đã gỡ, không còn subscription current.
    [Fact]
    public async Task None_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        StubExpireNoOp(userId);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync((UserSubscription?)null);

        var r = await _sut.getCurrentSubscription(userId);

        Assert.Null(r);
    }

    // Condition: còn gói active, map về DTO.
    [Fact]
    public async Task Active_ReturnsMapped()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan   = Plan(planId);
        var now    = DateTime.UtcNow;
        var sub = new UserSubscription
        {
            Id                 = Guid.NewGuid(),
            UserId             = userId,
            SubscriptionPlanId = planId,
            StartDate          = now.AddDays(-5),
            EndDate            = now.AddDays(25),
            Status             = 1,
            IsDeleted          = false,
            CreatedAt          = now.AddDays(-5),
            SubscriptionPlan   = plan
        };
        StubExpireNoOp(userId);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(sub);

        var r = await _sut.getCurrentSubscription(userId);

        Assert.NotNull(r);
        Assert.Equal("Đang hoạt động", r!.StatusLabel);
        Assert.Equal(plan.Name, r.Plan.Name);
        Assert.True(r.IsActive);
        Assert.True(r.RemainingDays > 0);
    }
}
