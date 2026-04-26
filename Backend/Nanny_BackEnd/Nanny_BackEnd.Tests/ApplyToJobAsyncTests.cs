using Moq;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

public class ApplyToJobAsyncTests
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

    public ApplyToJobAsyncTests()
    {
        _mockAppRepo = new Mock<IJobApplicationRepository>();
        _mockNotif   = new Mock<INotificationService>();
        _mockSubSvc  = new Mock<ISubscriptionService>();
        _sut         = new SearchService(_mockAppRepo.Object, _mockNotif.Object, _mockSubSvc.Object);

        _mockSubSvc.Setup(s => s.getBenefitsForNannyProfile(It.IsAny<Guid>()))
            .ReturnsAsync(new SubscriptionBenefitResponse { MonthlyApplicationLimit = 0 });
        _mockAppRepo.Setup(r => r.HasApprovedHealthCertificateAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
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
        VerificationStatus = (int)VerificationStatus.Approved,
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
        Title             = "Tìm bảo mẫu",
        Description       = "Mô tả công việc",
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

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplyToJobFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task IdentityNotVerified_ShouldReturnCombinedFailure()
    {
        var nanny = NannyOk();
        nanny.VerificationStatus = (int)VerificationStatus.Pending;
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(nanny);

        var result = await _sut.ApplyToJobAsync(_nannyUserId, _jobId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplyToJobFailure.MissingRequiredVerifications, result.Failure);
        _mockAppRepo.Verify(r => r.GetJobPostingForApplyAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HealthCertificateNotVerified_ShouldReturnCombinedFailure()
    {
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(NannyOk());
        _mockAppRepo.Setup(r => r.HasApprovedHealthCertificateAsync(_nannyProfileId, It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        var result = await _sut.ApplyToJobAsync(_nannyUserId, _jobId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplyToJobFailure.MissingRequiredVerifications, result.Failure);
        _mockAppRepo.Verify(r => r.GetJobPostingForApplyAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task JobNotAvailable()
    {
        var job = OpenJob();
        job.Status = (int)JobPostingStatus.Hidden;
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId)).ReturnsAsync(NannyOk());
        _mockAppRepo.Setup(r => r.GetJobPostingForApplyAsync(_jobId)).ReturnsAsync(job);

        var result = await _sut.ApplyToJobAsync(_nannyUserId, _jobId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplyToJobFailure.JobNotOpen, result.Failure);
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

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplyToJobFailure.AlreadyApplied, result.Failure);
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

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplyToJobFailure.MonthlyLimit, result.Failure);
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

        Assert.True(result.IsSuccess);
        _mockAppRepo.Verify(r => r.AddApplication(It.IsAny<JobApplication>()), Times.Once);
        _mockAppRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockNotif.Verify(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Exactly(2));
    }
}
