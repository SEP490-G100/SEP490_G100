using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
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

        var mockFavRepo      = new Mock<IFavoriteRepository>();
        var mockGeo          = new Mock<GeocodingService>(mockHttp.Object);
        var mockSubRepo      = new Mock<ISubscriptionRepository>();
        var mockUserRepo     = new Mock<IUserRepository>();
        var mockNotif        = new Mock<NotificationService>(mockSubRepo.Object, mockUserRepo.Object);
        var mockCasso        = new Mock<CassoService>(mockHttp.Object, Options.Create(new CassoOptions()));
        var mockPayOs        = new Mock<PayOsService>(mockHttp.Object, Options.Create(new PayOsOptions()));
        var mockSubService   = new Mock<SubscriptionService>(
            mockSubRepo.Object, mockNotif.Object, mockCasso.Object,
            mockPayOs.Object,   Options.Create(new PayOsOptions()));
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

    // ── TC1: Job không tồn tại → throw KeyNotFoundException ──────────────
    [Fact]
    public async Task NotFound()
    {
        var jobId = Guid.NewGuid();
        _mockJobRepo.Setup(r => r.hideExpiredPostings()).Returns(Task.CompletedTask);
        _mockJobRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync((JobPosting?)null);

        var act = () => _sut.getDetail(jobId);

        await act.Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Khong tim thay tin dang*");
    }

    // ── TC2: Job tồn tại → trả về đúng Id và Title ───────────────────────
    [Fact]
    public async Task Found()
    {
        var jobId = Guid.NewGuid();
        var job   = new JobPosting
        {
            Id    = jobId,
            Title = "Tìm người giữ trẻ",
            // Collections đã được khởi tạo sẵn trong model (= new List<>())
            // ParentProfile = null → null-safe trong mapToDetail
        };

        _mockJobRepo.Setup(r => r.hideExpiredPostings()).Returns(Task.CompletedTask);
        _mockJobRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);

        var result = await _sut.getDetail(jobId);

        result.Should().NotBeNull();
        result.Id.Should().Be(jobId);
        result.Title.Should().Be("Tìm người giữ trẻ");
    }
}
