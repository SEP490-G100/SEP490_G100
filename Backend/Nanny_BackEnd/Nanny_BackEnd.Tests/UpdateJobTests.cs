using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class UpdateJobTests
{
    private readonly Mock<IJobRepository>       _mockJobRepo;
    private readonly Mock<SubscriptionService> _mockSubSvc;
    private readonly JobService                _sut;

    public UpdateJobTests()
    {
        var mockHttp    = new Mock<System.Net.Http.IHttpClientFactory>();
        _mockJobRepo    = new Mock<IJobRepository>();
        var mockFavRepo = new Mock<IFavoriteRepository>();
        var mockGeo     = new Mock<GeocodingService>(mockHttp.Object);
        var mockSubRepo = new Mock<ISubscriptionRepository>();
        var mockUserRepo = new Mock<IUserRepository>();
        var mockNotif   = new Mock<NotificationService>(mockSubRepo.Object, mockUserRepo.Object);
        var mockCasso   = new Mock<CassoService>(mockHttp.Object, Options.Create(new CassoOptions()));
        var mockPayOs   = new Mock<PayOsService>(mockHttp.Object, Options.Create(new PayOsOptions()));
        _mockSubSvc     = new Mock<SubscriptionService>(
            mockSubRepo.Object, mockNotif.Object, mockCasso.Object,
            mockPayOs.Object,   Options.Create(new PayOsOptions()));
        var mockScope   = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        _sut = new JobService(
            _mockJobRepo.Object,
            mockFavRepo.Object,
            mockGeo.Object,
            _mockSubSvc.Object,
            mockNotif.Object,
            mockScope.Object,
            NullLogger<JobService>.Instance);
    }

    // -- Helpers -----------------------------------------------------------

    private static UpdateJobPostingRequest ValidRequest(int status = (int)JobPostingStatus.Public) => new()
    {
        JobType          = 1,
        SalaryNegotiable = true,
        Status           = status,
        Skills           = [],
        ScheduleSlots    = []
    };

    private static ParentProfile MakeParent(Guid parentProfileId) => new()
    {
        Id            = parentProfileId,
        UserId        = Guid.NewGuid(),
        ChildProfiles = new List<ChildProfile>
        {
            new() { Id = Guid.NewGuid(), IsDeleted = false, CreatedAt = DateTime.UtcNow }
        }
    };

    private void SetupCommon(Guid parentProfileId, JobPosting job, ParentProfile parent)
    {
        _mockSubSvc.Setup(s => s.getBenefitsForParentProfile(parentProfileId))
                   .ReturnsAsync(new SubscriptionBenefitResponse { ListingDurationDays = 30 });
        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentProfileId)).ReturnsAsync(parent);
        _mockJobRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockJobRepo.Setup(r => r.updateJobPosting(It.IsAny<JobPosting>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task JobNotFound()
    {
        var parentProfileId = Guid.NewGuid();
        var jobId           = Guid.NewGuid();

        _mockSubSvc.Setup(s => s.getBenefitsForParentProfile(parentProfileId))
                   .ReturnsAsync(new SubscriptionBenefitResponse { ListingDurationDays = 30 });
        _mockJobRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.updateJob(jobId, parentProfileId, ValidRequest()));
    }

    [Fact]
    public async Task UnauthorizedOwner()
    {
        var callerParentId = Guid.NewGuid();
        var job = new JobPosting
        {
            Id              = Guid.NewGuid(),
            ParentProfileId = Guid.NewGuid(),
            Title           = "Job gốc"
        };
        var parent = MakeParent(callerParentId);

        _mockSubSvc.Setup(s => s.getBenefitsForParentProfile(callerParentId))
                   .ReturnsAsync(new SubscriptionBenefitResponse { ListingDurationDays = 30 });
        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(callerParentId)).ReturnsAsync(parent);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.updateJob(job.Id, callerParentId, ValidRequest()));

    }

    // TC3: Status = Hidden → ClosedAt được set
    [Fact]
    public async Task StatusHidden_SetsClosedAt()
    {
        var parentProfileId = Guid.NewGuid();
        var job = new JobPosting
        {
            Id              = Guid.NewGuid(),
            ParentProfileId = parentProfileId,
            Title           = "Job gốc",
            ClosedAt        = null
        };
        var parent = MakeParent(parentProfileId);
        SetupCommon(parentProfileId, job, parent);

        await _sut.updateJob(job.Id, parentProfileId, ValidRequest(status: (int)JobPostingStatus.Hidden));

        Assert.NotNull(job.ClosedAt);
        Assert.Equal((int)JobPostingStatus.Hidden, job.Status);
    }

    // TC4: Status = Public → ModerationStatus reset về Pending
    [Fact]
    public async Task StatusPublic_ResetsModerationToPending()
    {
        var parentProfileId = Guid.NewGuid();
        var job = new JobPosting
        {
            Id               = Guid.NewGuid(),
            ParentProfileId  = parentProfileId,
            Title            = "Job gốc",
            ModerationStatus = (int)JobPostingModerationStatus.Approved,
            PublishedAt      = DateTime.UtcNow
        };
        var parent = MakeParent(parentProfileId);
        SetupCommon(parentProfileId, job, parent);

        await _sut.updateJob(job.Id, parentProfileId, ValidRequest(status: (int)JobPostingStatus.Public));

        Assert.Equal((int)JobPostingModerationStatus.Pending, job.ModerationStatus);
        Assert.Null(job.PublishedAt);
        Assert.Null(job.ClosedAt);
        Assert.Null(job.ModerationNote);
    }
}
