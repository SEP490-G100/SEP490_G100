using Moq;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>Unit tests cho <see cref="SearchService.WithdrawApplicationAsync"/> — mock <see cref="IJobApplicationRepository"/>.</summary>
public class WithdrawApplicationTests
{
    private readonly Mock<IJobApplicationRepository> _mockAppRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<ISubscriptionService>     _mockSubSvc;
    private readonly SearchService                 _sut;

    private readonly Guid _nannyUserId    = Guid.NewGuid();
    private readonly Guid _nannyProfileId = Guid.NewGuid();

    public WithdrawApplicationTests()
    {
        _mockAppRepo = new Mock<IJobApplicationRepository>();
        _mockNotif   = new Mock<INotificationService>();
        _mockSubSvc  = new Mock<ISubscriptionService>();
        _sut         = new SearchService(_mockAppRepo.Object, _mockNotif.Object, _mockSubSvc.Object);
    }

    [Fact]
    public async Task NotNanny_ReturnsNotNanny()
    {
        _mockAppRepo.Setup(r => r.GetNannyProfileIdByUserIdAsync(_nannyUserId))
            .ReturnsAsync((Guid?)null);

        var r = await _sut.WithdrawApplicationAsync(_nannyUserId, Guid.NewGuid());

        Assert.False(r.IsSuccess);
        Assert.Equal(WithdrawForNannyFailure.NotNanny, r.Failure);
    }

    [Fact]
    public async Task ApplicationNotFound_ReturnsNotFound()
    {
        _mockAppRepo.Setup(r => r.GetNannyProfileIdByUserIdAsync(_nannyUserId))
            .ReturnsAsync(_nannyProfileId);
        _mockAppRepo.Setup(r => r.GetApplicationForWithdrawAsync(It.IsAny<Guid>(), _nannyProfileId))
            .ReturnsAsync((JobApplication?)null);

        var r = await _sut.WithdrawApplicationAsync(_nannyUserId, Guid.NewGuid());

        Assert.False(r.IsSuccess);
        Assert.Equal(WithdrawForNannyFailure.NotFound, r.Failure);
    }

    [Fact]
    public async Task NotPending_ReturnsNotPending()
    {
        _mockAppRepo.Setup(r => r.GetNannyProfileIdByUserIdAsync(_nannyUserId))
            .ReturnsAsync(_nannyProfileId);
        _mockAppRepo.Setup(r => r.GetApplicationForWithdrawAsync(It.IsAny<Guid>(), _nannyProfileId))
            .ReturnsAsync(new JobApplication
            {
                Id             = Guid.NewGuid(),
                Status         = 1,
                NannyProfileId = _nannyProfileId
            });

        var r = await _sut.WithdrawApplicationAsync(_nannyUserId, Guid.NewGuid());

        Assert.False(r.IsSuccess);
        Assert.Equal(WithdrawForNannyFailure.NotPending, r.Failure);
    }

    [Fact]
    public async Task Success_WithdrawsPendingApplication()
    {
        var app = new JobApplication
        {
            Id             = Guid.NewGuid(),
            JobPostingId   = Guid.NewGuid(),
            NannyProfileId = _nannyProfileId,
            Status         = 0,
            IsDeleted      = false,
            CreatedAt      = DateTime.UtcNow
        };
        _mockAppRepo.Setup(r => r.GetNannyProfileIdByUserIdAsync(_nannyUserId))
            .ReturnsAsync(_nannyProfileId);
        _mockAppRepo.Setup(r => r.GetApplicationForWithdrawAsync(app.Id, _nannyProfileId))
            .ReturnsAsync(app);

        var r = await _sut.WithdrawApplicationAsync(_nannyUserId, app.Id);

        Assert.True(r.IsSuccess);
        Assert.Equal(3, app.Status);
        Assert.NotNull(app.WithdrawnAt);
        _mockAppRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}
