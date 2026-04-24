using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class DeleteJobTests
{
    private readonly Mock<IJobRepository> _mockJobRepo;
    private readonly JobService          _sut;

    public DeleteJobTests()
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
        var mockSubSvc  = new Mock<SubscriptionService>(
            mockSubRepo.Object, mockNotif.Object, mockCasso.Object,
            mockPayOs.Object,   Options.Create(new PayOsOptions()));
        var mockScope   = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        _sut = new JobService(
            _mockJobRepo.Object,
            mockFavRepo.Object,
            mockGeo.Object,
            mockSubSvc.Object,
            mockNotif.Object,
            mockScope.Object,
            NullLogger<JobService>.Instance);
    }

    // ── Helper ────────────────────────────────────────────────────────────
    private static JobPosting MakeJob(Guid parentProfileId, List<JobApplication>? applications = null) => new()
    {
        Id              = Guid.NewGuid(),
        ParentProfileId = parentProfileId,
        Title           = "Tìm bảo mẫu",
        JobApplications = applications ?? new List<JobApplication>()
    };

    // ── TC1: Job không tồn tại → KeyNotFoundException ─────────────────────
    [Fact]
    public async Task JobNotFound()
    {
        _mockJobRepo.Setup(r => r.viewDetailPosting(It.IsAny<Guid>()))
                    .ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.deletePost(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Contains("tìm thấy tin đăng", ex.Message);
    }

    // ── TC2: ParentProfileId không khớp → UnauthorizedAccessException ─────
    [Fact]
    public async Task UnauthorizedOwner()
    {
        var job = MakeJob(parentProfileId: Guid.NewGuid());

        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.deletePost(job.Id, Guid.NewGuid()));
        Assert.Contains("quyền xóa", ex.Message);
    }

    // ── TC3: Có đơn ứng tuyển đang chờ (Pending) → InvalidOperationException
    [Fact]
    public async Task HasPendingApplications()
    {
        var parentProfileId = Guid.NewGuid();
        var job = MakeJob(parentProfileId, applications: new List<JobApplication>
        {
            new() { Status = 0 }   // 0 = Pending
        });

        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.deletePost(job.Id, parentProfileId));
        Assert.Contains("ứng tuyển chờ xét duyệt", ex.Message);
    }

    // ── TC4: Hợp lệ → deleteJobPosting được gọi đúng 1 lần ──────────────
    [Fact]
    public async Task Success()
    {
        var parentProfileId = Guid.NewGuid();
        var job = MakeJob(parentProfileId);   // không có đơn pending

        _mockJobRepo.Setup(r => r.viewDetailPosting(job.Id)).ReturnsAsync(job);
        _mockJobRepo.Setup(r => r.deleteJobPosting(job)).Returns(Task.CompletedTask);

        await _sut.deletePost(job.Id, parentProfileId);

        _mockJobRepo.Verify(r => r.deleteJobPosting(job), Times.Once);
    }
}
