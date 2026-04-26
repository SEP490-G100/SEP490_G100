using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.SubscriptionController.GetTransactionHistory"/> → <see cref="SubscriptionService.getTransactionHistory"/>.
/// </summary>
public class GetTransactionHistoryTests
{
    private readonly Mock<ISubscriptionRepository> _mockRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<INotificationService>  _mockNotif;
    private readonly Mock<ICassoService>          _mockCasso;
    private readonly Mock<IPayOsService>          _mockPayOs;
    private readonly SubscriptionService          _sut;

    public GetTransactionHistoryTests()
    {
        _mockRepo  = new Mock<ISubscriptionRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockNotif = new Mock<INotificationService>();
        _mockCasso = new Mock<ICassoService>();
        _mockPayOs = new Mock<IPayOsService>();
        var payOpt = Options.Create(new PayOsOptions { ExpiresAfterMinutes = 15 });
        _sut = new SubscriptionService(
            _mockRepo.Object,
            _mockUserRepo.Object,
            _mockNotif.Object,
            _mockCasso.Object,
            _mockPayOs.Object,
            payOpt,
            NullLogger<SubscriptionService>.Instance);
    }

    [Fact]
    public async Task NoTransactions_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        _mockRepo.Setup(r => r.getPendingSubscriptionTransactions(userId))
            .ReturnsAsync(new List<Transaction>());
        _mockRepo.Setup(r => r.getUserSubscriptionTransactions(userId, 30))
            .ReturnsAsync(new List<Transaction>());

        var list = await _sut.getTransactionHistory(userId);

        Assert.Empty(list);
        _mockRepo.Verify(r => r.saveChanges(), Times.Never);
        _mockRepo.Verify(r => r.getUserSubscriptionTransactions(userId, 30), Times.Once);
    }

    [Fact]
    public async Task ReturnsMappedRows_WithStatusAndTypeLabels()
    {
        var userId    = Guid.NewGuid();
        var id        = Guid.NewGuid();
        var created   = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2025, 4, 1, 0, 5, 0, DateTimeKind.Utc);
        var tx = new Transaction
        {
            Id                        = id,
            UserId                    = userId,
            Amount                    = 199000m,
            Status                    = 2,
            Type                      = 1,
            Description               = "Thanh toán gói Plus",
            PaymentGatewayTransactionId = "PAY-1",
            CreatedAt                 = created,
            CompletedAt               = completed
        };

        _mockRepo.Setup(r => r.getPendingSubscriptionTransactions(userId))
            .ReturnsAsync(new List<Transaction>());
        _mockRepo.Setup(r => r.getUserSubscriptionTransactions(userId, 30))
            .ReturnsAsync(new List<Transaction> { tx });

        var list = await _sut.getTransactionHistory(userId);

        var r = Assert.Single(list);
        Assert.Equal(id, r.Id);
        Assert.Equal(199000m, r.Amount);
        Assert.Equal(2, r.Status);
        Assert.Equal(1, r.Type);
        Assert.Equal("Thanh toán gói Plus", r.Description);
        Assert.Equal("PAY-1", r.PaymentGatewayTransactionId);
        Assert.Equal(created, r.CreatedAt);
        Assert.Equal(completed, r.CompletedAt);
    }


    [Fact]
    public async Task ExpiresOldPending_ThenReturnsUpdatedRow()
    {
        var userId = Guid.NewGuid();
        var tx = new Transaction
        {
            Id       = Guid.NewGuid(),
            UserId   = userId,
            Amount   = 1,
            Status    = 1,
            Type      = 1,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockRepo.Setup(r => r.getPendingSubscriptionTransactions(userId))
            .ReturnsAsync(new List<Transaction> { tx });
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockRepo.Setup(r => r.getUserSubscriptionTransactions(userId, 30))
            .ReturnsAsync(new List<Transaction> { tx });

        var list = await _sut.getTransactionHistory(userId);

        _mockRepo.Verify(r => r.saveChanges(), Times.Once);
        Assert.Equal(3, tx.Status);
        var r = Assert.Single(list);
        Assert.Equal(3, r.Status);
        Assert.Equal("Thất bại", r.StatusLabel);
    }
}
