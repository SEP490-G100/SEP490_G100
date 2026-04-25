using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.SubscriptionController.Subscribe"/> → <see cref="SubscriptionService.subscribe"/>.
/// </summary>
public class SubscribeTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly Mock<INotificationService>      _mockNotif;
    private readonly Mock<ICassoService>          _mockCasso;
    private readonly Mock<IPayOsService>         _mockPayOs;
    private readonly SubscriptionService         _sut;

    public SubscribeTests()
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

    private void StubExpireOnly(Guid userId) =>
        _mockRepo.Setup(r => r.getExpiredActiveSubscriptions(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new List<UserSubscription>());

    private static SubscriptionPlan ParentishPlan(Guid id) => new()
    {
        Id            = id,
        Name          = "Gói phụ huynh bài đăng",
        Description   = "test",
        Features      = "parent; job",
        Price         = 99000m,
        DurationDays  = 30,
        IsActive      = true,
        IsDeleted     = false,
        CreatedAt     = DateTime.UtcNow
    };

    [Fact]
    public async Task PlanNotFound_Throws()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        StubExpireOnly(userId);
        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync((SubscriptionPlan?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.subscribe(userId, new SubscribeRequest { SubscriptionPlanId = planId }));
    }

    [Fact]
    public async Task NotParent_Throws()
    {
        var userId = Guid.NewGuid();
        var plan   = ParentishPlan(Guid.NewGuid());
        StubExpireOnly(userId);
        _mockRepo.Setup(r => r.findPlanById(plan.Id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.hasNannyProfile(userId)).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.subscribe(userId, new SubscribeRequest { SubscriptionPlanId = plan.Id }));
    }

    [Fact]
    public async Task HasActiveSubscription_Throws()
    {
        var userId = Guid.NewGuid();
        var plan   = ParentishPlan(Guid.NewGuid());
        StubExpireOnly(userId);
        _mockRepo.Setup(r => r.findPlanById(plan.Id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new UserSubscription
            {
                Id      = Guid.NewGuid(),
                UserId  = userId,
                Status  = 1,
                EndDate = DateTime.UtcNow.AddDays(5)
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.subscribe(userId, new SubscribeRequest { SubscriptionPlanId = plan.Id }));
    }

    [Fact]
    public async Task Success_AddsTransactionAndSubscription_ReturnsMapping()
    {
        var userId = Guid.NewGuid();
        var plan   = ParentishPlan(Guid.NewGuid());
        StubExpireOnly(userId);
        _mockRepo.Setup(r => r.findPlanById(plan.Id)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync((UserSubscription?)null);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);

        var r = await _sut.subscribe(userId, new SubscribeRequest
        {
            SubscriptionPlanId         = plan.Id,
            PaymentGatewayTransactionId = "  bank-ref-1  "
        });

        Assert.Equal(1, r.Status);
        Assert.Equal(plan.Name, r.Plan.Name);
        _mockRepo.Verify(x => x.addTransaction(It.Is<Transaction>(t =>
            t.UserId == userId
            && t.Status == 2
            && t.Amount == plan.Price
            && t.PaymentGatewayTransactionId == "bank-ref-1")), Times.Once);
        _mockRepo.Verify(x => x.addUserSubscription(It.Is<UserSubscription>(s =>
            s.UserId == userId && s.SubscriptionPlanId == plan.Id && s.Status == 1)), Times.Once);
        _mockRepo.Verify(x => x.saveChanges(), Times.Once);
    }
}
