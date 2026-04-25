using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// </summary>
public class CreatePaymentTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly Mock<INotificationService>      _mockNotif;
    private readonly Mock<ICassoService>           _mockCasso;
    private readonly Mock<IPayOsService>            _mockPayOs;
    private readonly IOptions<PayOsOptions>         _payOpt;
    private readonly SubscriptionService           _sut;

    public CreatePaymentTests()
    {
        _mockRepo  = new Mock<ISubscriptionRepository>();
        _mockNotif = new Mock<INotificationService>();
        _mockCasso = new Mock<ICassoService>();
        _mockPayOs = new Mock<IPayOsService>();
        _payOpt    = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });
        _sut = new SubscriptionService(
            _mockRepo.Object,
            _mockNotif.Object,
            _mockCasso.Object,
            _mockPayOs.Object,
            _payOpt);
    }

    private void BaseExpireStubs(Guid userId)
    {
        _mockRepo.Setup(r => r.getExpiredActiveSubscriptions(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new List<UserSubscription>());
        _mockRepo.Setup(r => r.getPendingSubscriptionTransactions(userId))
            .ReturnsAsync(new List<Transaction>());
    }

    private static CreateSubscriptionPaymentRequest Req(Guid planId) =>
        new() { SubscriptionPlanId = planId };

    private static SubscriptionPlan ParentishPlan(Guid id) => new()
    {
        Id            = id,
        Name          = "Goi phu huynh can dang bai",
        Description   = "test",
        Features      = "parent; job post",
        Price         = 199000m,
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
        BaseExpireStubs(userId);
        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync((SubscriptionPlan?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.createPayment(userId, Req(planId)));
    }

    [Fact]
    public async Task NotParent_Throws()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan   = ParentishPlan(planId);
        BaseExpireStubs(userId);
        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(false);
        _mockRepo.Setup(r => r.hasNannyProfile(userId)).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.createPayment(userId, Req(planId)));
    }

    [Fact]
    public async Task HasActiveSubscription_Throws()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan   = ParentishPlan(planId);
        BaseExpireStubs(userId);
        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new UserSubscription
            {
                Id        = Guid.NewGuid(),
                UserId    = userId,
                EndDate   = DateTime.UtcNow.AddDays(10),
                Status    = 1
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.createPayment(userId, Req(planId)));
    }

    [Fact]
    public async Task Success_CreatesSession_ReturnsInstruction()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan   = ParentishPlan(planId);
        BaseExpireStubs(userId);
        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync((UserSubscription?)null);
        _mockRepo.Setup(r => r.existsGatewayTransactionCode(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockPayOs.Setup(p => p.getPaymentInstruction(It.IsAny<int>()))
            .ReturnsAsync((PayOsPaymentInstruction?)null);
        _mockPayOs.Setup(p => p.createPaymentInstruction(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync((Guid tid, int orderCode, decimal amount, string planName) => new PayOsPaymentInstruction
            {
                OrderCode       = orderCode,
                Amount          = amount,
                TransferContent = "PAY-TEST",
                QrPayload       = "raw",
                QrCodeUrl       = "q.png",
                CheckoutUrl     = "https://pay.test/checkout",
                BankId          = "VCCB",
                AccountNumber   = "123",
                AccountName     = "Test Co",
                PaymentLinkId   = "pl-1",
                Status          = "PENDING",
                ExpiresAt       = DateTime.UtcNow.AddMinutes(15)
            });

        var r = await _sut.createPayment(userId, Req(planId));

        Assert.NotEqual(Guid.Empty, r.TransactionId);
        Assert.Equal("PAYOS", r.PaymentMethod);
        Assert.Equal(plan.Name, r.PlanName);
        Assert.Equal(199000m, r.Amount);
        _mockRepo.Verify(x => x.addTransaction(It.Is<Transaction>(t =>
            t.UserId == userId && t.Status == 1 && t.Type == 1 && t.Amount == plan.Price)), Times.Once);
        _mockPayOs.Verify(p => p.createPaymentInstruction(It.IsAny<Guid>(), It.IsAny<int>(), 199000m, plan.Name), Times.Once);
    }

    [Fact]
    public async Task PayOsThrows_MarksTransactionFailed()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan   = ParentishPlan(planId);
        BaseExpireStubs(userId);
        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync((UserSubscription?)null);
        _mockRepo.Setup(r => r.existsGatewayTransactionCode(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockPayOs.Setup(p => p.getPaymentInstruction(It.IsAny<int>()))
            .ReturnsAsync((PayOsPaymentInstruction?)null);
        _mockPayOs.Setup(p => p.createPaymentInstruction(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("PayOS offline"));

        Transaction? added = null;
        _mockRepo.Setup(r => r.addTransaction(It.IsAny<Transaction>()))
            .Callback<Transaction>(t => added = t);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.createPayment(userId, Req(planId)));

        Assert.NotNull(added);
        Assert.Equal(3, added!.Status);
        _mockRepo.Verify(r => r.saveChanges(), Times.AtLeast(2));
    }
}
