using Moq;
using FluentAssertions;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>Unit tests cho <see cref="SearchService.ReviewJobApplicationAsync"/> — mock <see cref="IJobApplicationRepository"/>.</summary>
public class ReviewJobApplicationTests
{
    private readonly Mock<IJobApplicationRepository> _mockAppRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<ISubscriptionService>     _mockSubSvc;
    private readonly SearchService                 _sut;

    private readonly Guid _parentUserId    = Guid.NewGuid();
    private readonly Guid _parentProfileId = Guid.NewGuid();
    private readonly Guid _nannyUserId     = Guid.NewGuid();
    private readonly Guid _nannyProfileId  = Guid.NewGuid();

    public ReviewJobApplicationTests()
    {
        _mockAppRepo = new Mock<IJobApplicationRepository>();
        _mockNotif   = new Mock<INotificationService>();
        _mockSubSvc  = new Mock<ISubscriptionService>();
        _sut         = new SearchService(_mockAppRepo.Object, _mockNotif.Object, _mockSubSvc.Object);

        _mockNotif.Setup(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
    }

    private JobApplication PendingApplication()
    {
        var job = new JobPosting
        {
            Id              = Guid.NewGuid(),
            ParentProfileId = _parentProfileId,
            Title           = "Tim bao mau",
            Description     = "Mo ta",
            IsDeleted       = false
        };
        return new JobApplication
        {
            Id             = Guid.NewGuid(),
            JobPostingId   = job.Id,
            NannyProfileId = _nannyProfileId,
            Status         = 0,
            IsDeleted      = false,
            CreatedAt      = DateTime.UtcNow,
            JobPosting     = job,
            NannyProfile = new NannyProfile
            {
                Id     = _nannyProfileId,
                UserId = _nannyUserId,
                User = new User
                {
                    Id = _nannyUserId, FirstName = "Nanny", LastName = "One", Email = "n@test.com"
                }
            }
        };
    }

    [Fact]
    public async Task InvalidAction_ReturnsBadInput()
    {
        var r = await _sut.ReviewJobApplicationAsync(
            _parentUserId,
            Guid.NewGuid(),
            new ReviewJobApplicationRequestDto { Action = 99 });

        r.IsSuccess.Should().BeFalse();
        r.Failure.Should().Be(ReviewJobParentFailure.BadInput);
    }

    [Fact]
    public async Task RejectWithoutReason_ReturnsBadInput()
    {
        var r = await _sut.ReviewJobApplicationAsync(
            _parentUserId,
            Guid.NewGuid(),
            new ReviewJobApplicationRequestDto { Action = 2, RejectionReason = "  " });

        r.IsSuccess.Should().BeFalse();
        r.Failure.Should().Be(ReviewJobParentFailure.BadInput);
    }

    [Fact]
    public async Task ApplicationNotFound_ReturnsApplicationNotFound()
    {
        _mockAppRepo.Setup(x => x.GetParentProfileIdByUserIdAsync(_parentUserId))
            .ReturnsAsync(_parentProfileId);
        _mockAppRepo.Setup(x => x.GetApplicationForReviewAsync(It.IsAny<Guid>()))
            .ReturnsAsync((JobApplication?)null);

        var r = await _sut.ReviewJobApplicationAsync(
            _parentUserId,
            Guid.NewGuid(),
            new ReviewJobApplicationRequestDto { Action = 1 });

        r.IsSuccess.Should().BeFalse();
        r.Failure.Should().Be(ReviewJobParentFailure.ApplicationNotFound);
    }

    [Fact]
    public async Task AlreadyReviewed_ReturnsAlreadyProcessed()
    {
        var app = PendingApplication();
        app.Status = 1;
        _mockAppRepo.Setup(x => x.GetParentProfileIdByUserIdAsync(_parentUserId))
            .ReturnsAsync(_parentProfileId);
        _mockAppRepo.Setup(x => x.GetApplicationForReviewAsync(app.Id))
            .ReturnsAsync(app);

        var r = await _sut.ReviewJobApplicationAsync(
            _parentUserId,
            app.Id,
            new ReviewJobApplicationRequestDto { Action = 1 });

        r.IsSuccess.Should().BeFalse();
        r.Failure.Should().Be(ReviewJobParentFailure.AlreadyProcessed);
    }

    [Fact]
    public async Task Accept_ReturnsSuccess_NoNotification()
    {
        var app = PendingApplication();
        _mockAppRepo.Setup(x => x.GetParentProfileIdByUserIdAsync(_parentUserId))
            .ReturnsAsync(_parentProfileId);
        _mockAppRepo.Setup(x => x.GetApplicationForReviewAsync(app.Id))
            .ReturnsAsync(app);

        var r = await _sut.ReviewJobApplicationAsync(
            _parentUserId,
            app.Id,
            new ReviewJobApplicationRequestDto { Action = 1 });

        r.IsSuccess.Should().BeTrue();
        app.Status.Should().Be(1);
        app.ReviewedAt.Should().NotBeNull();
        _mockAppRepo.Verify(x => x.SaveChangesAsync(), Times.Once);
        _mockNotif.Verify(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task Reject_ReturnsSuccess_SendsNotificationToNanny()
    {
        var app = PendingApplication();
        _mockAppRepo.Setup(x => x.GetParentProfileIdByUserIdAsync(_parentUserId))
            .ReturnsAsync(_parentProfileId);
        _mockAppRepo.Setup(x => x.GetApplicationForReviewAsync(app.Id))
            .ReturnsAsync(app);

        var r = await _sut.ReviewJobApplicationAsync(
            _parentUserId,
            app.Id,
            new ReviewJobApplicationRequestDto { Action = 2, RejectionReason = "Khong phu hop" });

        r.IsSuccess.Should().BeTrue();
        app.Status.Should().Be(2);
        app.RejectionReason.Should().Be("Khong phu hop");
        _mockNotif.Verify(n => n.createNotification(
            _nannyUserId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Once);
    }
}
