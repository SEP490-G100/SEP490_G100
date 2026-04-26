using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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
            mockSubRepo.Object, mockUserRepo.Object, mockNotif.Object, mockCasso.Object,
            mockPayOs.Object,   Options.Create(new PayOsOptions()),
            NullLogger<SubscriptionService>.Instance);
        var mockScope   = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        _sut = new JobService(
            _mockJobRepo.Object,
            _mockFavRepo.Object,
            mockGeo.Object,
            mockSubSvc.Object,
            mockNotif.Object,
            mockScope.Object,
            NullLogger<JobService>.Instance);

        _mockJobRepo.Setup(r => r.hideExpiredPostings()).Returns(Task.CompletedTask);
    }

    private static SearchJobRequest DefaultFilters(int page = 1, int pageSize = 20) =>
        new() { Page = page, PageSize = pageSize };

    private static JobPosting MakeJob(string title = "Tìm người giúp việc") => new()
    {
        Id    = Guid.NewGuid(),
        Title = title
    };

    [Fact]
    public async Task ReturnsJobsFromRepo()
    {
        var job = MakeJob();
        _mockJobRepo.Setup(r => r.searchJobPosting(
            It.IsAny<SearchJobRequest>(), It.IsAny<Guid?>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<JobPosting> { job });

        var result = await _sut.findJobs(DefaultFilters(), null);

        Assert.Single(result);
        Assert.Equal(job.Id, result[0].Id);
        Assert.Equal("Tìm người giúp việc", result[0].Title);
        Assert.False(result[0].IsFavorite);
    }

    // Boundary: pageSize vượt trần 50.
    [Fact]
    public async Task PageSizeAboveMax_Clamped()
    {
        _mockJobRepo.Setup(r => r.searchJobPosting(
            It.Is<SearchJobRequest>(f => f.PageSize == 50), It.IsAny<Guid?>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<JobPosting>());

        var result = await _sut.findJobs(DefaultFilters(pageSize: 100));

        Assert.Empty(result);
    }

    [Fact]
    public async Task NoNannyProfileId_IsFavoriteFalse()
    {
        var job = MakeJob();
        _mockJobRepo.Setup(r => r.searchJobPosting(
            It.IsAny<SearchJobRequest>(), It.IsAny<Guid?>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<JobPosting> { job });

        var result = await _sut.findJobs(DefaultFilters(), currentNannyProfileId: null);

        Assert.Single(result);
        Assert.False(result[0].IsFavorite);
    }

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

        Assert.Single(result);
        Assert.True(result[0].IsFavorite);
    }
}
