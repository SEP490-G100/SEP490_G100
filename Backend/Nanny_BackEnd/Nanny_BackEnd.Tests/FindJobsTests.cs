using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class FindJobsTests
{
    private readonly Mock<IJobRepository>      _mockJobRepo;
    private readonly Mock<IFavoriteRepository> _mockFavRepo;
    private readonly JobService               _sut;

    public FindJobsTests()
    {
        var mockHttp    = new Mock<System.Net.Http.IHttpClientFactory>();
        _mockJobRepo    = new Mock<IJobRepository>();
        _mockFavRepo    = new Mock<IFavoriteRepository>();
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
            _mockFavRepo.Object,
            mockGeo.Object,
            mockSubSvc.Object,
            mockNotif.Object,
            mockScope.Object,
            NullLogger<JobService>.Instance);

        // hideExpiredPostings luôn no-op trong test
        _mockJobRepo.Setup(r => r.hideExpiredPostings()).Returns(Task.CompletedTask);
    }

    // ── Helper ────────────────────────────────────────────────────────────
    private static SearchJobRequest DefaultFilters(int page = 1, int pageSize = 20) =>
        new() { Page = page, PageSize = pageSize };

    private static JobPosting MakeJob(string title = "Tìm bảo mẫu") => new()
    {
        Id    = Guid.NewGuid(),
        Title = title
    };

    // ── TC1: PageSize > 50 → clamp về 50, repo được gọi với PageSize = 50 ─
    [Fact]
    public async Task PageSizeAboveMax_Clamped()
    {
        var filters = DefaultFilters(pageSize: 100);

        _mockJobRepo.Setup(r => r.searchJobPosting(
            It.Is<SearchJobRequest>(f => f.PageSize == 50), It.IsAny<Guid?>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<JobPosting>());

        await _sut.findJobs(filters);

        _mockJobRepo.Verify(r => r.searchJobPosting(
            It.Is<SearchJobRequest>(f => f.PageSize == 50),
            It.IsAny<Guid?>(), It.IsAny<bool>()), Times.Once);
    }

    // ── TC3: Không có nannyProfileId → getFavoriteJobIds không được gọi,
    //         IsFavorite = false ──────────────────────────────────────────
    [Fact]
    public async Task NoNannyProfileId_SkipsFavorites()
    {
        var job = MakeJob();
        _mockJobRepo.Setup(r => r.searchJobPosting(
            It.IsAny<SearchJobRequest>(), It.IsAny<Guid?>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<JobPosting> { job });

        var result = await _sut.findJobs(DefaultFilters(), currentNannyProfileId: null);

        result.Should().HaveCount(1);
        result[0].IsFavorite.Should().BeFalse();

        _mockFavRepo.Verify(r => r.getFavoriteJobIds(
            It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>()),
            Times.Never);
    }

    // ── TC4: Có nannyProfileId, job nằm trong favorites → IsFavorite = true
    [Fact]
    public async Task WithNannyProfileId_JobInFavorites_IsFavoriteTrue()
    {
        var job          = MakeJob();
        var nannyProfile = Guid.NewGuid();

        _mockJobRepo.Setup(r => r.searchJobPosting(
            It.IsAny<SearchJobRequest>(), It.IsAny<Guid?>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<JobPosting> { job });

        _mockFavRepo.Setup(r => r.getFavoriteJobIds(nannyProfile, It.IsAny<IEnumerable<Guid>>()))
                    .ReturnsAsync(new HashSet<Guid> { job.Id });

        var result = await _sut.findJobs(DefaultFilters(), currentNannyProfileId: nannyProfile);

        result.Should().HaveCount(1);
        result[0].IsFavorite.Should().BeTrue();
    }
}
