using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Xunit;

namespace Nanny_BackEnd.Tests;

public class SubscriptionJobTests
{
    [Fact]
    public async Task FreeParent_IsLimitedToTwoPostsPerMonth()
    {
        await using var fixture = await TestFixture.create();

        var request = new CreateJobPostingRequest
        {
            Title = "Can bao mau cho be 2 tuoi",
            Description = "Can tim bao mau co kinh nghiem cho be 2 tuoi vao buoi toi.",
            JobType = 1,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Dong Da",
            Status = 1
        };

        await fixture.JobService.createJob(fixture.FreeParentProfileId, request);
        await fixture.JobService.createJob(fixture.FreeParentProfileId, request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.JobService.createJob(fixture.FreeParentProfileId, request));

        Assert.Contains("2 bai viet", ex.Message);
    }

    [Fact]
    public async Task PlusParent_GetsThreePostsAndFortyFiveDayListing()
    {
        await using var fixture = await TestFixture.create();
        await fixture.subscribePlan(fixture.PlusParentUserId, "PLUS");

        var jobId = await fixture.JobService.createJob(fixture.PlusParentProfileId, new CreateJobPostingRequest
        {
            Title = "Can bao mau Plus",
            Description = "Gia dinh can bao mau theo gio cho be mau giao va don dep nhe.",
            JobType = 2,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Cau Giay",
            Status = 1
        });

        var job = await fixture.Db.JobPostings.FirstAsync(j => j.Id == jobId);
        Assert.NotNull(job.ExpiresAt);
        Assert.InRange((job.ExpiresAt!.Value - job.CreatedAt).TotalDays, 44.9, 45.1);

        for (var i = 0; i < 2; i++)
        {
            await fixture.JobService.createJob(fixture.PlusParentProfileId, new CreateJobPostingRequest
            {
                Title = $"Can bao mau Plus {i}",
                Description = $"Mo ta cong viec hop le cho bai Plus thu {i} de kiem tra gioi han so bai.",
                JobType = 1,
                NumberOfChildren = 1,
                SalaryNegotiable = true,
                City = "Ha Noi",
                District = "Cau Giay",
                Status = 1
            });
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.JobService.createJob(fixture.PlusParentProfileId, new CreateJobPostingRequest
            {
                Title = "Vuot gioi han Plus lan 4",
                Description = "Bai thu tu se phai bi chan theo gioi han cua goi Plus hien tai.",
                JobType = 1,
                NumberOfChildren = 1,
                SalaryNegotiable = true,
                City = "Ha Noi",
                District = "Cau Giay",
                Status = 1
            }));

        Assert.Contains("3 bai viet", ex.Message);
    }

    [Fact]
    public async Task ProJobs_AreRankedAheadAndMarkedAsPriority()
    {
        await using var fixture = await TestFixture.create();
        await fixture.subscribePlan(fixture.ProParentUserId, "PRO");

        await fixture.JobService.createJob(fixture.FreeParentProfileId, new CreateJobPostingRequest
        {
            Title = "Bai free",
            Description = "Bai free dung de so sanh thu tu hien thi trong ket qua tim kiem.",
            JobType = 1,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Ba Dinh",
            Status = 1
        });

        await Task.Delay(20);

        await fixture.JobService.createJob(fixture.ProParentProfileId, new CreateJobPostingRequest
        {
            Title = "Bai Pro",
            Description = "Bai Pro duoc ky vong uu tien hien thi truoc bai thuong trong tim kiem.",
            JobType = 1,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Ba Dinh",
            Status = 1
        });

        var results = await fixture.JobService.findJobs(new SearchJobRequest
        {
            City = "Ha Noi",
            Page = 1,
            PageSize = 10
        });

        Assert.NotEmpty(results);
        Assert.Equal("Bai Pro", results[0].Title);
        Assert.Equal("PRO", results[0].SubscriptionPlanCode);
        Assert.True(results[0].FeaturedBadge);
        Assert.True(results[0].SearchPriority);
    }

    [Fact]
    public async Task ManagedPlans_IncludeParentAndNannyPlusPro()
    {
        await using var fixture = await TestFixture.create();

        var plans = await fixture.SubscriptionService.getPlans();
        var codes = plans.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("PLUS", codes);
        Assert.Contains("PRO", codes);
        Assert.Contains("NANNY_PLUS", codes);
        Assert.Contains("NANNY_PRO", codes);
    }

    [Fact]
    public async Task ParentCannotSubscribeToNannyPlan()
    {
        await using var fixture = await TestFixture.create();

        var plans = await fixture.SubscriptionService.getPlans();
        var nannyPlan = plans.Single(p => p.Code == "NANNY_PLUS");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.SubscriptionService.subscribe(fixture.FreeParentUserId, new SubscribeRequest
            {
                SubscriptionPlanId = nannyPlan.Id,
                PaymentGatewayTransactionId = "invalid-parent-nanny-plan"
            }));

        Assert.Contains("khong phai Nanny", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NannyPlus_GetsThreeApplicationsPerMonth()
    {
        await using var fixture = await TestFixture.create();
        await fixture.subscribePlan(fixture.PlusNannyUserId, "NANNY_PLUS");

        var benefits = await fixture.SubscriptionService.getBenefitsForNannyProfile(fixture.PlusNannyProfileId);

        Assert.Equal(3, benefits.MonthlyApplicationLimit);
        Assert.True(benefits.FeaturedBadge);
        Assert.False(benefits.SearchPriority);
    }

    [Fact]
    public async Task SubscriptionReminder_CreatesSevenAndThreeDayNotifications_WithoutDuplicates()
    {
        await using var fixture = await TestFixture.create();
        await fixture.subscribePlan(fixture.PlusParentUserId, "PLUS");

        var subscription = await fixture.Db.UserSubscriptions
            .Include(s => s.SubscriptionPlan)
            .FirstAsync(s => s.UserId == fixture.PlusParentUserId);

        subscription.EndDate = DateTime.UtcNow.Date.AddDays(7);
        await fixture.Db.SaveChangesAsync();

        var createdFirstRun = await fixture.NotificationService.createSubscriptionExpiryReminders();
        var createdSecondRun = await fixture.NotificationService.createSubscriptionExpiryReminders();

        var notifications = await fixture.Db.Notifications
            .Where(n => n.UserId == fixture.PlusParentUserId)
            .ToListAsync();

        Assert.Equal(1, createdFirstRun);
        Assert.Equal(0, createdSecondRun);
        Assert.Single(notifications);
        Assert.Contains("7 ngay", notifications[0].Title);
        Assert.Equal(1, notifications[0].Type);
        Assert.Null(notifications[0].CreatedBy);

        subscription.EndDate = DateTime.UtcNow.Date.AddDays(3);
        await fixture.Db.SaveChangesAsync();

        var createdThirdRun = await fixture.NotificationService.createSubscriptionExpiryReminders();
        notifications = await fixture.Db.Notifications
            .Where(n => n.UserId == fixture.PlusParentUserId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        Assert.Equal(1, createdThirdRun);
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.Title.Contains("3 ngay"));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private TestFixture(
            Sep490NannyDbContext db,
            SubscriptionService subscriptionService,
            JobService jobService,
            NotificationService notificationService)
        {
            Db = db;
            SubscriptionService = subscriptionService;
            JobService = jobService;
            NotificationService = notificationService;
        }

        public Sep490NannyDbContext Db { get; }
        public SubscriptionService SubscriptionService { get; }
        public JobService JobService { get; }
        public NotificationService NotificationService { get; }
        public Guid FreeParentUserId { get; private set; }
        public Guid FreeParentProfileId { get; private set; }
        public Guid PlusParentUserId { get; private set; }
        public Guid PlusParentProfileId { get; private set; }
        public Guid ProParentUserId { get; private set; }
        public Guid ProParentProfileId { get; private set; }
        public Guid PlusNannyUserId { get; private set; }
        public Guid PlusNannyProfileId { get; private set; }

        public static async Task<TestFixture> create()
        {
            var options = new DbContextOptionsBuilder<Sep490NannyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new Sep490NannyDbContext(options);
            var favoriteRepo = new FavoriteRepository(db);
            var jobRepo = new JobRepository(db);
            var subscriptionRepo = new SubscriptionRepository(db);
            var notificationService = new NotificationService(subscriptionRepo);
            var vietQrService = new VietQrService(
                new FakeHttpClientFactory(),
                Options.Create(new VietQrOptions
                {
                    BaseUrl = "https://api.vietqr.io/v2/",
                    ClientId = "test-client",
                    ApiKey = "test-key",
                    SuccessUrl = "https://example.test/success",
                    CancelUrl = "https://example.test/cancel"
                }));
            var subscriptionService = new SubscriptionService(subscriptionRepo, notificationService, vietQrService);
            var geo = new GeocodingService(new FakeHttpClientFactory());
            var jobService = new JobService(jobRepo, favoriteRepo, geo, subscriptionService);

            var fixture = new TestFixture(db, subscriptionService, jobService, notificationService);
            await fixture.seedUsers();
            return fixture;
        }

        public async Task subscribePlan(Guid userId, string planCode)
        {
            var plans = await SubscriptionService.getPlans();
            var plan = plans.Single(p => p.Code == planCode);
            await SubscriptionService.subscribe(userId, new SubscribeRequest
            {
                SubscriptionPlanId = plan.Id,
                PaymentGatewayTransactionId = $"{planCode}-{userId}"
            });
        }

        private async Task seedUsers()
        {
            (FreeParentUserId, FreeParentProfileId) = await addParent("free@nanny.vn", "Free");
            (PlusParentUserId, PlusParentProfileId) = await addParent("plus@nanny.vn", "Plus");
            (ProParentUserId, ProParentProfileId) = await addParent("pro@nanny.vn", "Pro");
            (PlusNannyUserId, PlusNannyProfileId) = await addNanny("nannyplus@nanny.vn", "Plus");
        }

        private async Task<(Guid UserId, Guid ParentProfileId)> addParent(string email, string firstName)
        {
            var userId = Guid.NewGuid();
            var parentProfileId = Guid.NewGuid();
            Db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FirstName = firstName,
                LastName = "Parent",
                Status = 1,
                AuthProvider = 0,
                EmailConfirmed = true,
                PhoneConfirmed = false,
                CreatedAt = DateTime.UtcNow
            });
            Db.ParentProfiles.Add(new ParentProfile
            {
                Id = parentProfileId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            await Db.SaveChangesAsync();
            return (userId, parentProfileId);
        }

        private async Task<(Guid UserId, Guid NannyProfileId)> addNanny(string email, string firstName)
        {
            var userId = Guid.NewGuid();
            var nannyProfileId = Guid.NewGuid();
            Db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FirstName = firstName,
                LastName = "Nanny",
                Status = 1,
                AuthProvider = 0,
                EmailConfirmed = true,
                PhoneConfirmed = false,
                CreatedAt = DateTime.UtcNow
            });
            Db.NannyProfiles.Add(new NannyProfile
            {
                Id = nannyProfileId,
                UserId = userId,
                SalaryType = 1,
                VerificationStatus = 0,
                TotalReviews = 0,
                ProfileCompleteness = 0,
                CreatedAt = DateTime.UtcNow
            });

            await Db.SaveChangesAsync();
            return (userId, nannyProfileId);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FakeHttpMessageHandler())
        {
            BaseAddress = new Uri("https://example.test")
        };
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            });
    }
}
