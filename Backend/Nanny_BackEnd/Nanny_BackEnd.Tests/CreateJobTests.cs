using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class CreateJobTests
{
    private readonly Mock<IJobRepository>          _mockJobRepo;
    private readonly Mock<SubscriptionService>    _mockSubService;
    private readonly Mock<NotificationService>    _mockNotif;
    private readonly Mock<GeocodingService>       _mockGeo;
    private readonly JobService                   _sut;

    private static readonly SubscriptionBenefitResponse PaidBenefits = new()
    {
        ListingDurationDays = 30,
        MonthlyJobPostLimit = 0
    };

    public CreateJobTests()
    {
        var mockHttp = new Mock<System.Net.Http.IHttpClientFactory>();

        _mockJobRepo    = new Mock<IJobRepository>();
        _mockGeo        = new Mock<GeocodingService>(mockHttp.Object);

        var mockFavRepo   = new Mock<IFavoriteRepository>();
        var mockSubRepo   = new Mock<ISubscriptionRepository>();
        var mockUserRepo  = new Mock<IUserRepository>();
        _mockNotif        = new Mock<NotificationService>(mockSubRepo.Object, mockUserRepo.Object);
        var mockCasso     = new Mock<CassoService>(mockHttp.Object, Options.Create(new CassoOptions()));
        var mockPayOs     = new Mock<PayOsService>(mockHttp.Object, Options.Create(new PayOsOptions()));
        _mockSubService   = new Mock<SubscriptionService>(
            mockSubRepo.Object, _mockNotif.Object, mockCasso.Object,
            mockPayOs.Object,   Options.Create(new PayOsOptions()));
        var mockScope     = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        _sut = new JobService(
            _mockJobRepo.Object,
            mockFavRepo.Object,
            _mockGeo.Object,
            _mockSubService.Object,
            _mockNotif.Object,
            mockScope.Object,
            NullLogger<JobService>.Instance);
    }

    private static ParentProfile MakeParent(Guid? parentProfileId = null) => new()
    {
        Id      = parentProfileId ?? Guid.NewGuid(),
        UserId  = Guid.NewGuid(),
        User    = new User { FirstName = "Test", LastName = "Parent" },
        ChildProfiles = new List<ChildProfile>
        {
            new() { Id = Guid.NewGuid(), IsDeleted = false, CreatedAt = DateTime.UtcNow }
        }
    };

    // Helper: request hợp lệ tối giản
    private static CreateJobPostingRequest ValidRequest() => new()
    {
        JobType         = 1,
        SalaryNegotiable = true,       // bỏ qua salary validation
        Status          = 1,
        Skills          = [],          // empty → syncRequirements chỉ gọi saveChanges
        ScheduleSlots   = []           // empty → syncScheduleRequirements chỉ gọi saveChanges
    };

    [Fact]
    public async Task ParentNotFound()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
                       .ReturnsAsync(PaidBenefits);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
                    .ReturnsAsync((ParentProfile?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.createJob(parentId, ValidRequest()));
    }

    [Fact]
    public async Task FreeUserPostingLimitReached()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
                       .ReturnsAsync(SubscriptionBenefitResponse.FreeParent);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
                    .ReturnsAsync(MakeParent(parentId));
        _mockSubService.Setup(s => s.hasActiveParentSubscription(parentId))
                       .ReturnsAsync(false);
        _mockJobRepo.Setup(r => r.countActiveJobPostings(parentId))
                    .ReturnsAsync(3);   // == freeParentActivePostingLimit

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.createJob(parentId, ValidRequest()));
    }

    [Fact]
    public async Task FreeUserMonthlyLimitReached()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
                       .ReturnsAsync(SubscriptionBenefitResponse.FreeParent);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
                    .ReturnsAsync(MakeParent(parentId));
        _mockSubService.Setup(s => s.hasActiveParentSubscription(parentId))
                       .ReturnsAsync(false);
        _mockJobRepo.Setup(r => r.countActiveJobPostings(parentId))
                    .ReturnsAsync(1);   // dưới giới hạn active
        _mockJobRepo.Setup(r => r.countJobPostingsInCurrentMonth(parentId))
                    .ReturnsAsync(3);   // == MonthlyJobPostLimit = 3

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.createJob(parentId, ValidRequest()));
    }

    // TC4: Paid user, tất cả hợp lệ → trả về JobId
    [Fact]
    public async Task Success()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
                       .ReturnsAsync(PaidBenefits);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
                    .ReturnsAsync(MakeParent(parentId));
        _mockSubService.Setup(s => s.hasActiveParentSubscription(parentId))
                        .ReturnsAsync(true);    // paid → bỏ qua free limit checks
        _mockGeo.Setup(g => g.geocode(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(default((decimal, decimal)?));
        _mockJobRepo.Setup(r => r.createJobPosting(It.IsAny<JobPosting>()))
                    .ReturnsAsync((JobPosting j) => j);
        _mockJobRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockNotif.Setup(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
        _mockNotif.Setup(n => n.createNotificationForModerators(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        var jobId = await _sut.createJob(parentId, ValidRequest());

        Assert.NotEqual(Guid.Empty, jobId);
        _mockJobRepo.Verify(r => r.createJobPosting(It.IsAny<JobPosting>()), Times.Once);
    }

    private void SetupSuccessPath(Guid parentId)
    {
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
                       .ReturnsAsync(SubscriptionBenefitResponse.FreeParent);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
                    .ReturnsAsync(MakeParent(parentId));
        _mockSubService.Setup(s => s.hasActiveParentSubscription(parentId))
                       .ReturnsAsync(false);
        _mockGeo.Setup(g => g.geocode(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(default((decimal, decimal)?));
        _mockJobRepo.Setup(r => r.createJobPosting(It.IsAny<JobPosting>()))
                    .ReturnsAsync((JobPosting j) => j);
        _mockJobRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockNotif.Setup(n => n.createNotification(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
        _mockNotif.Setup(n => n.createNotificationForModerators(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
    }

    // Boundary TC1: active = 2 (ngay dưới giới hạn 3) → vẫn tạo được
    [Fact]
    public async Task FreeUser_ActivePostings_AtBoundary_Pass()
    {
        var parentId = Guid.NewGuid();
        SetupSuccessPath(parentId);
        _mockJobRepo.Setup(r => r.countActiveJobPostings(parentId)).ReturnsAsync(2);
        _mockJobRepo.Setup(r => r.countJobPostingsInCurrentMonth(parentId)).ReturnsAsync(1);

        var jobId = await _sut.createJob(parentId, ValidRequest());

        Assert.NotEqual(Guid.Empty, jobId);
    }

    // Boundary TC2: monthly = 2 (ngay dưới giới hạn 3) → vẫn tạo được
    [Fact]
    public async Task FreeUser_MonthlyPostings_AtBoundary_Pass()
    {
        var parentId = Guid.NewGuid();
        SetupSuccessPath(parentId);
        _mockJobRepo.Setup(r => r.countActiveJobPostings(parentId)).ReturnsAsync(1);
        _mockJobRepo.Setup(r => r.countJobPostingsInCurrentMonth(parentId)).ReturnsAsync(2);

        var jobId = await _sut.createJob(parentId, ValidRequest());

        Assert.NotEqual(Guid.Empty, jobId);
    }


    [Fact]
    public async Task NannyAgeMinGreaterThanMax_Throws()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
            .ReturnsAsync(PaidBenefits);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
            .ReturnsAsync(MakeParent(parentId));

        var req = ValidRequest();
        req.MinNannyAge = 50;
        req.MaxNannyAge = 20;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.createJob(parentId, req));
    }

    [Fact]
    public async Task SalaryMinOutOfVndRange_Throws()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
            .ReturnsAsync(PaidBenefits);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
            .ReturnsAsync(MakeParent(parentId));

        var req = ValidRequest();
        req.SalaryMin = 1_000_000m;
        req.SalaryMax = 9_000_000m;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.createJob(parentId, req));
        Assert.Contains("8.000.000", ex.Message);
    }

    [Fact]
    public async Task SalaryMinGreaterThanMax_Throws()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
            .ReturnsAsync(PaidBenefits);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
            .ReturnsAsync(MakeParent(parentId));

        var req = ValidRequest();
        req.SalaryMin = 20_000_000m;
        req.SalaryMax = 10_000_000m;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.createJob(parentId, req));
    }

    [Fact]
    public async Task NotNegotiable_RequiresMinSalary_Throws()
    {
        var parentId = Guid.NewGuid();
        _mockSubService.Setup(s => s.getBenefitsForParentProfile(parentId))
            .ReturnsAsync(PaidBenefits);
        _mockJobRepo.Setup(r => r.getParentProfileSnapshot(parentId))
            .ReturnsAsync(MakeParent(parentId));
        _mockSubService.Setup(s => s.hasActiveParentSubscription(parentId))
            .ReturnsAsync(true);

        var req = ValidRequest();
        req.SalaryNegotiable = false;
        req.SalaryMin = null;
        req.SalaryMax = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.createJob(parentId, req));

    }
}
