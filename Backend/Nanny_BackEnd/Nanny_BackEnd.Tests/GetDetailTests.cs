using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class GetDetailTests
{
    private readonly Mock<IJobRepository> _mockJobRepo;
    private readonly JobService          _sut;

    public GetDetailTests()
    {
        var mockHttp = new Mock<System.Net.Http.IHttpClientFactory>();

        _mockJobRepo = new Mock<IJobRepository>();
        _mockJobRepo.Setup(r => r.GetApprovedPublicJobsMissingExpiryAsync()).ReturnsAsync(new List<JobPosting>());
        _mockJobRepo.Setup(r => r.hideExpiredPostings()).ReturnsAsync(new List<JobPosting>());

        var mockFavRepo      = new Mock<IFavoriteRepository>();
        var mockGeo          = new Mock<GeocodingService>(mockHttp.Object);
        var mockSubRepo      = new Mock<ISubscriptionRepository>();
        var mockUserRepo     = new Mock<IUserRepository>();
        var mockNotif        = new Mock<NotificationService>(mockSubRepo.Object, mockUserRepo.Object);
        var mockCasso        = new Mock<CassoService>(mockHttp.Object, Options.Create(new CassoOptions()));
        var mockPayOs        = new Mock<PayOsService>(mockHttp.Object, Options.Create(new PayOsOptions()));
        var mockSubService   = new Mock<SubscriptionService>(
            mockSubRepo.Object, mockUserRepo.Object, mockNotif.Object, mockCasso.Object,
            mockPayOs.Object,   Options.Create(new PayOsOptions()),
            NullLogger<SubscriptionService>.Instance);
        var mockScopeFactory = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        _sut = new JobService(
            _mockJobRepo.Object,
            mockFavRepo.Object,
            mockGeo.Object,
            mockSubService.Object,
            mockNotif.Object,
            mockScopeFactory.Object,
            NullLogger<JobService>.Instance);
    }

    [Fact]
    public async Task NotFound()
    {
        var jobId = Guid.NewGuid();
        _mockJobRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.getDetail(jobId));
    }

    [Fact]
    public async Task Found()
    {
        var jobId = Guid.NewGuid();
        var job   = new JobPosting
        {
            Id    = jobId,
            // ParentProfile = null → null-safe trong mapToDetail
        };

        _mockJobRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);

        var result = await _sut.getDetail(jobId);

        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
    }
}
