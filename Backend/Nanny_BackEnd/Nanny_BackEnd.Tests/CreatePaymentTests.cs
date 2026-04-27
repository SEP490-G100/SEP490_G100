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
/// </summary>
public class CreatePaymentTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<INotificationService>      _mockNotif;
    private readonly Mock<ICassoService>           _mockCasso;
    private readonly Mock<IPayOsService>            _mockPayOs;
    private readonly IOptions<PayOsOptions>         _payOpt;
    private readonly SubscriptionService           _sut;

    public CreatePaymentTests()
    {
        _mockRepo  = new Mock<ISubscriptionRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockNotif = new Mock<INotificationService>();
        _mockCasso = new Mock<ICassoService>();
        _mockPayOs = new Mock<IPayOsService>();
        _payOpt    = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });
        _sut = new SubscriptionService(
            _mockRepo.Object,
            _mockUserRepo.Object,
            _mockNotif.Object,
            _mockCasso.Object,
            _mockPayOs.Object,
            _payOpt,
            NullLogger<SubscriptionService>.Instance);
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

    [Fact]
    public async Task ReusePendingSession_WhenLookupMissingQrPayload_UsesStoredQrMetadata()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan = ParentishPlan(planId);
        var existingOrderCode = 223344556;
        var existingTransactionId = Guid.NewGuid();
        const string qrPayload = "00020101021238570010A000000727012700069704360110123456789012345802VN53037045405199005802VN5912NANNYMATCH6005HANOI6304ABCD";

        _mockRepo.Setup(r => r.getExpiredActiveSubscriptions(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new List<UserSubscription>());

        var pending = new List<Transaction>
        {
            new()
            {
                Id = existingTransactionId,
                UserId = userId,
                Amount = plan.Price,
                PaymentGatewayTransactionId = existingOrderCode.ToString(),
                Status = 1,
                Type = 1,
                CreatedAt = DateTime.UtcNow,
                Description = $"NM GOI_PHU_HUYNH_CAN_DANG_BAI {existingOrderCode} | PAYOS_BANK:VCB | PAYOS_ACC:0123456789 | PAYOS_NAME:NANNYMATCH | PAYOS_QR:{qrPayload}"
            }
        };
        _mockRepo.Setup(r => r.getPendingSubscriptionTransactions(userId))
            .ReturnsAsync(pending);

        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync((UserSubscription?)null);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);

        _mockPayOs.Setup(p => p.getPaymentInstruction(existingOrderCode))
            .ReturnsAsync(new PayOsPaymentInstruction
            {
                OrderCode = existingOrderCode,
                Amount = plan.Price,
                TransferContent = $"NM{existingOrderCode}",
                QrPayload = "",
                QrCodeUrl = "https://api.qrserver.com/v1/create-qr-code/?size=320x320&data=https%3A%2F%2Fpay.payos.vn%2Fweb%2Fold",
                CheckoutUrl = "https://pay.payos.vn/web/old",
                BankId = "",
                AccountNumber = "",
                AccountName = "",
                PaymentLinkId = "pl-old",
                Status = "PENDING",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            });

        var result = await _sut.createPayment(userId, Req(planId));

        Assert.Equal(existingTransactionId, result.TransactionId);
        Assert.Equal(qrPayload, result.QrPayload);
        Assert.Contains(Uri.EscapeDataString(qrPayload), result.QrCodeUrl);
        _mockPayOs.Verify(p => p.createPaymentInstruction(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReusePendingSession_WhenQrCannotBeRecovered_CreatesNewSession()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan = ParentishPlan(planId);
        var oldOrderCode = 334455667;
        var oldTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = plan.Price,
            PaymentGatewayTransactionId = oldOrderCode.ToString(),
            Status = 1,
            Type = 1,
            CreatedAt = DateTime.UtcNow,
            Description = $"NM GOI_PHU_HUYNH_CAN_DANG_BAI {oldOrderCode} | PAYOS_BANK:VCB | PAYOS_ACC:0123456789 | PAYOS_NAME:NANNYMATCH"
        };

        _mockRepo.Setup(r => r.getExpiredActiveSubscriptions(userId, It.IsAny<DateTime>()))
            .ReturnsAsync(new List<UserSubscription>());
        _mockRepo.Setup(r => r.getPendingSubscriptionTransactions(userId))
            .ReturnsAsync(new List<Transaction> { oldTransaction });
        _mockRepo.Setup(r => r.findPlanById(planId)).ReturnsAsync(plan);
        _mockRepo.Setup(r => r.hasParentProfile(userId)).ReturnsAsync(true);
        _mockRepo.Setup(r => r.findCurrentSubscription(userId, It.IsAny<DateTime>()))
            .ReturnsAsync((UserSubscription?)null);
        _mockRepo.Setup(r => r.existsGatewayTransactionCode(It.IsAny<string>())).ReturnsAsync(false);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);

        _mockPayOs.Setup(p => p.getPaymentInstruction(It.IsAny<int>()))
            .ReturnsAsync((int orderCode) =>
                orderCode == oldOrderCode
                    ? new PayOsPaymentInstruction
                    {
                        OrderCode = oldOrderCode,
                        Amount = plan.Price,
                        TransferContent = $"NM{oldOrderCode}",
                        QrPayload = "",
                        QrCodeUrl = "https://api.qrserver.com/v1/create-qr-code/?size=320x320&data=https%3A%2F%2Fpay.payos.vn%2Fweb%2Fold",
                        CheckoutUrl = "https://pay.payos.vn/web/old",
                        BankId = "",
                        AccountNumber = "",
                        AccountName = "",
                        PaymentLinkId = "pl-old",
                        Status = "PENDING",
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    }
                    : null);

        _mockPayOs.Setup(p => p.createPaymentInstruction(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync((Guid tid, int orderCode, decimal amount, string planName) => new PayOsPaymentInstruction
            {
                OrderCode = orderCode,
                Amount = amount,
                TransferContent = "PAY-NEW",
                QrPayload = "NEW_QR_PAYLOAD",
                QrCodeUrl = "https://api.qrserver.com/v1/create-qr-code/?size=320x320&data=NEW_QR_PAYLOAD",
                CheckoutUrl = "https://pay.payos.vn/web/new",
                BankId = "VCB",
                AccountNumber = "0123",
                AccountName = "NANNYMATCH",
                PaymentLinkId = "pl-new",
                Status = "PENDING",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });

        Transaction? newTransaction = null;
        _mockRepo.Setup(r => r.addTransaction(It.IsAny<Transaction>()))
            .Callback<Transaction>(t => newTransaction = t);

        var result = await _sut.createPayment(userId, Req(planId));

        Assert.Equal(3, oldTransaction.Status);
        Assert.NotNull(newTransaction);
        Assert.Equal(newTransaction!.Id, result.TransactionId);
        Assert.Equal("NEW_QR_PAYLOAD", result.QrPayload);
        _mockRepo.Verify(r => r.addTransaction(It.IsAny<Transaction>()), Times.Once);
    }
}
