using Moq;
using FluentAssertions;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>Unit tests cho <see cref="SearchService.ApplyToJobAsync"/> — mock <see cref="IJobApplicationRepository"/>.</summary>
public class ApplyJobTests
{
    private readonly Mock<IJobApplicationRepository> _mockAppRepo;
    private readonly Mock<INotificationService>      _mockNotif;
    private readonly Mock<ISubscriptionService>     _mockSubSvc;
    private readonly SearchService                  _sut;

    private readonly Guid _nannyUserId     = Guid.NewGuid();
    private readonly Guid _nannyProfileId  = Guid.NewGuid();
    private readonly Guid _parentUserId    = Guid.NewGuid();
    private readonly Guid _parentProfileId = Guid.NewGuid();
    private readonly Guid _jobId           = Guid.NewGuid();

    public ApplyJobTests()
    {
        _mockAppRepo = new Mock<IJobApplicationRepository>();
        _mockNotif   = new Mock<INotificationService>();
        _mockSubSvc  = new Mock<ISubscriptionService>();
        _sut         = new SearchService(_mockAppRepo.Object, _mockNotif.Object, _mockSubSvc.Object);

        _mockSubSvc.Setup(s => s.getBenefitsForNannyProfile(It.IsAny<Guid>()))
            .ReturnsAsync(new SubscriptionBenefitResponse { MonthlyApplicationLimit = 0 });
        _mockNotif.Setup(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
    }

    private NannyProfile NannyOk() => new()
    {
        Id        = _nannyProfileId,
        UserId    = _nannyUserId,
        IsDeleted = false,
        User = new User
        {
            Id        = _nannyUserId,
            FirstName = "Test",
            LastName  = "Nanny",
            Email     = "nanny@test.com",
            CreatedAt = DateTime.UtcNow
        }
    };

    private JobPosting OpenJob() => new()
    {
        Id                = _jobId,
        ParentProfileId   = _parentProfileId,
        Title             = "Tim bao mau",
        Description       = "Mo ta cong viec",
        Status            = (int)JobPostingStatus.Public,
        ModerationStatus  = (int)JobPostingModerationStatus.Approved,
        IsDeleted         = false,
        ParentProfile = new ParentProfile
        {
            Id     = _parentProfileId,
            UserId = _parentUserId
        }
    };

    [Fact]
    public async Task JobNotFound()
    {
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(NannyOk());
        _mockAppRepo.Setup(r => r.GetJobPostingForApplyAsync(It.IsAny<Guid>())).ReturnsAsync((JobPosting?)null);

        var result = await _sut.ApplyToJobAsync(_nannyUserId, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ApplyToJobFailure.NotFound);
    }

    [Fact]
    public async Task JobNotAvailable()
    {
        var job = OpenJob();
        job.Status = (int)JobPostingStatus.Hidden;
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(NannyOk());
        _mockAppRepo.Setup(r => r.GetJobPostingForApplyAsync(_jobId)).ReturnsAsync(job);

        var result = await _sut.ApplyToJobAsync(_nannyUserId, _jobId);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ApplyToJobFailure.JobNotOpen);
    }

    [Fact]
    public async Task AlreadyApplied_NotWithdrawn()
    {
        var job = OpenJob();
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(NannyOk());
        _mockAppRepo.Setup(r => r.GetJobPostingForApplyAsync(_jobId)).ReturnsAsync(job);
        _mockAppRepo.Setup(r => r.GetExistingApplicationAsync(_jobId, _nannyProfileId))
            .ReturnsAsync(new JobApplication
            {
                Status    = 0,
                CreatedAt = DateTime.UtcNow
            });

        var result = await _sut.ApplyToJobAsync(_nannyUserId, _jobId);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ApplyToJobFailure.AlreadyApplied);
    }

    [Fact]
    public async Task MonthlyLimitReached()
    {
        var job = OpenJob();
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(NannyOk());
        _mockAppRepo.Setup(r => r.GetJobPostingForApplyAsync(_jobId)).ReturnsAsync(job);
        _mockAppRepo.Setup(r => r.GetExistingApplicationAsync(_jobId, _nannyProfileId))
            .ReturnsAsync((JobApplication?)null);
        _mockSubSvc.Setup(s => s.getBenefitsForNannyProfile(_nannyProfileId))
            .ReturnsAsync(SubscriptionBenefitResponse.FreeNanny);
        _mockAppRepo.Setup(r => r.CountMonthlyApplicationsAsync(_nannyProfileId, It.IsAny<DateTime>()))
            .ReturnsAsync(2);

        var result = await _sut.ApplyToJobAsync(_nannyUserId, _jobId);

        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().Be(ApplyToJobFailure.MonthlyLimit);
    }

    [Fact]
    public async Task Success_NewApplication()
    {
        var job = OpenJob();
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(NannyOk());
        _mockAppRepo.Setup(r => r.GetJobPostingForApplyAsync(_jobId)).ReturnsAsync(job);
        _mockAppRepo.Setup(r => r.GetExistingApplicationAsync(_jobId, _nannyProfileId))
            .ReturnsAsync((JobApplication?)null);
        _mockAppRepo.Setup(r => r.CountMonthlyApplicationsAsync(_nannyProfileId, It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var result = await _sut.ApplyToJobAsync(_nannyUserId, _jobId);

        result.IsSuccess.Should().BeTrue();
        _mockAppRepo.Verify(r => r.AddApplication(It.IsAny<JobApplication>()), Times.Once);
        _mockAppRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockNotif.Verify(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Exactly(2));
    }
}
