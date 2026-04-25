using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class ReviewJobAsyncTests
{
    private readonly Mock<IJobRepository>       _mockJobRepo;
    private readonly Mock<NotificationService> _mockNotif;
    private readonly JobService                _sut;

    public ReviewJobAsyncTests()
    {
        var mockHttp = new Mock<System.Net.Http.IHttpClientFactory>();

        _mockJobRepo = new Mock<IJobRepository>();

        var mockFavRepo   = new Mock<IFavoriteRepository>();
        var mockGeo       = new Mock<GeocodingService>(mockHttp.Object);
        var mockSubRepo   = new Mock<ISubscriptionRepository>();
        var mockUserRepo  = new Mock<IUserRepository>();
        _mockNotif        = new Mock<NotificationService>(mockSubRepo.Object, mockUserRepo.Object);
        var mockCasso     = new Mock<CassoService>(mockHttp.Object, Options.Create(new CassoOptions()));
        var mockPayOs     = new Mock<PayOsService>(mockHttp.Object, Options.Create(new PayOsOptions()));
        var mockSubSvc    = new Mock<SubscriptionService>(
            mockSubRepo.Object, _mockNotif.Object, mockCasso.Object,
            mockPayOs.Object,   Options.Create(new PayOsOptions()));
        var mockScope     = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        _sut = new JobService(
            _mockJobRepo.Object,
            mockFavRepo.Object,
            mockGeo.Object,
            mockSubSvc.Object,
            _mockNotif.Object,
            mockScope.Object,
            NullLogger<JobService>.Instance);
    }

    // -- Helper ------------------------------------------------------------
    private static JobPosting MakeJob(
        int status = (int)JobPostingStatus.Public,
        int moderationStatus = (int)JobPostingModerationStatus.Pending,
        ParentProfile? parent = null) => new()
    {
        Id = Guid.NewGuid(),
        Status = status,
        ModerationStatus = moderationStatus,
        ParentProfile = parent
                      ?? new ParentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() }
    };

    private void SetupNotif() =>
        _mockNotif.Setup(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task NotFound()
    {
        var jobId = Guid.NewGuid();
        _mockJobRepo.Setup(r => r.viewDetailPosting(jobId))
                    .ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.ReviewJobAsync(jobId, Guid.NewGuid(),
            (int)JobPostingModerationStatus.Approved, null));
    }

    // TC2: Approved + Public job
    [Fact]
    public async Task Approved_PublicJob_SetsPublishedAt()
    {
        var job = MakeJob(
            status: (int)JobPostingStatus.Public,
            parent: new ParentProfile { UserId = Guid.NewGuid() });
        var moderatorId = Guid.NewGuid();

        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);
        _mockJobRepo.Setup(r => r.updateJobPosting(job)).Returns(Task.CompletedTask);
        SetupNotif();

        await _sut.ReviewJobAsync(job.Id, moderatorId,
            (int)JobPostingModerationStatus.Approved, null);

        Assert.Equal((int)JobPostingModerationStatus.Approved, job.ModerationStatus);
        Assert.NotNull(job.PublishedAt);
        Assert.Null(job.ClosedAt);
        Assert.Equal(moderatorId, job.ModeratedBy);

        _mockNotif.Verify(n => n.createNotification(
            job.ParentProfile!.UserId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), moderatorId),
            Times.Once);
    }

    // TC3: Approved + Hidden job
    [Fact]
    public async Task Approved_HiddenJob_SetsClosedAt()
    {
        var job = MakeJob(
            status: (int)JobPostingStatus.Hidden,
            parent: new ParentProfile { UserId = Guid.NewGuid() });

        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);
        _mockJobRepo.Setup(r => r.updateJobPosting(job)).Returns(Task.CompletedTask);
        SetupNotif();

        await _sut.ReviewJobAsync(job.Id, Guid.NewGuid(),
            (int)JobPostingModerationStatus.Approved, null);

        Assert.Null(job.PublishedAt);
        Assert.NotNull(job.ClosedAt);
    }

    [Fact]
    public async Task Rejected_WithNote_SetsClosedAt_TrimsNote()
    {
        var job = MakeJob(
            status: (int)JobPostingStatus.Public,
            parent: new ParentProfile { UserId = Guid.NewGuid() });

        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);
        _mockJobRepo.Setup(r => r.updateJobPosting(job)).Returns(Task.CompletedTask);
        SetupNotif();

        await _sut.ReviewJobAsync(job.Id, Guid.NewGuid(),
            (int)JobPostingModerationStatus.Rejected, "  Nội dung vi phạm  ");

        Assert.Equal((int)JobPostingModerationStatus.Rejected, job.ModerationStatus);
        Assert.Null(job.PublishedAt);
        Assert.NotNull(job.ClosedAt);
        Assert.Equal("Nội dung vi phạm", job.ModerationNote);
    }
}
