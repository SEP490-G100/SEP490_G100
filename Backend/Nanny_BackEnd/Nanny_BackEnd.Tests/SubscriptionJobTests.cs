using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
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
    public async Task PlusParent_GetsTenPostsAndFortyFiveDayListing()
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

        for (var i = 0; i < 9; i++)
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
                Title = "Vuot gioi han Plus",
                Description = "Bai thu muoi mot se phai bi chan theo gioi han cua goi Plus hien tai.",
                JobType = 1,
                NumberOfChildren = 1,
                SalaryNegotiable = true,
                City = "Ha Noi",
                District = "Cau Giay",
                Status = 1
            }));

        Assert.Contains("10 bai viet", ex.Message);
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

    private sealed class TestFixture : IAsyncDisposable
    {
        private TestFixture(
            Sep490NannyDbContext db,
            SubscriptionService subscriptionService,
            JobService jobService)
        {
            Db = db;
            SubscriptionService = subscriptionService;
            JobService = jobService;
        }

        public Sep490NannyDbContext Db { get; }
        public SubscriptionService SubscriptionService { get; }
        public JobService JobService { get; }
        public Guid FreeParentUserId { get; private set; }
        public Guid FreeParentProfileId { get; private set; }
        public Guid PlusParentUserId { get; private set; }
        public Guid PlusParentProfileId { get; private set; }
        public Guid ProParentUserId { get; private set; }
        public Guid ProParentProfileId { get; private set; }

        public static async Task<TestFixture> create()
        {
            var options = new DbContextOptionsBuilder<Sep490NannyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new Sep490NannyDbContext(options);
            var favoriteRepo = new FavoriteRepository(db);
            var jobRepo = new JobRepository(db);
            var subscriptionRepo = new SubscriptionRepository(db);
            var subscriptionService = new SubscriptionService(subscriptionRepo);
            var geo = new GeocodingService(new FakeHttpClientFactory());
            var jobService = new JobService(jobRepo, favoriteRepo, geo, subscriptionService);

            var fixture = new TestFixture(db, subscriptionService, jobService);
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
