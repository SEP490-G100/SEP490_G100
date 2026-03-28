using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Xunit;

namespace Nanny_BackEnd.Tests;

public class SubscriptionJobTests
{
    [Fact]
    public async Task FreeParent_IsLimitedToThreePostsPerMonth()
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
        await fixture.JobService.createJob(fixture.FreeParentProfileId, request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.JobService.createJob(fixture.FreeParentProfileId, request));

        Assert.Contains("3 bai", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FreeParent_CannotExceedThreeActivePosts_WithoutSubscription()
    {
        await using var fixture = await TestFixture.create();

        var request = new CreateJobPostingRequest
        {
            Title = "Can bao mau gio han active",
            Description = "Kiem tra gioi han bai dang active cua Parent free.",
            JobType = 1,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Dong Da",
            Status = (int)JobPostingStatus.Public
        };

        for (var i = 0; i < 3; i++)
        {
            await fixture.JobService.createJob(fixture.FreeParentProfileId, request);
        }

        var previousMonth = DateTime.UtcNow.AddMonths(-1);
        var freeParentJobs = await fixture.Db.JobPostings
            .Where(j => j.ParentProfileId == fixture.FreeParentProfileId && !j.IsDeleted)
            .ToListAsync();
        foreach (var job in freeParentJobs)
            job.CreatedAt = previousMonth;
        await fixture.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.JobService.createJob(fixture.FreeParentProfileId, request));

        Assert.Contains("mien phi", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3 bai dang", ex.Message, StringComparison.OrdinalIgnoreCase);
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

        var freeJobId = await fixture.JobService.createJob(fixture.FreeParentProfileId, new CreateJobPostingRequest
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

        var proJobId = await fixture.JobService.createJob(fixture.ProParentProfileId, new CreateJobPostingRequest
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

        await fixture.JobService.moderateJob(freeJobId, Guid.NewGuid(), true, null);
        await fixture.JobService.moderateJob(proJobId, Guid.NewGuid(), true, null);

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
    public async Task EditToHidden_KeepsApprovedModeration_AndStillAppearsInHistory()
    {
        await using var fixture = await TestFixture.create();

        var jobId = await fixture.JobService.createJob(fixture.FreeParentProfileId, new CreateJobPostingRequest
        {
            Title = "Bai can an",
            Description = "Bai nay duoc tao de kiem tra viec chuyen sang hidden khi edit.",
            JobType = 1,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Ba Dinh",
            Status = (int)JobPostingStatus.Public
        });

        var moderatorId = Guid.NewGuid();
        await fixture.JobService.moderateJob(jobId, moderatorId, true, null);

        await fixture.JobService.updateJob(jobId, fixture.FreeParentProfileId, new UpdateJobPostingRequest
        {
            Title = "Bai can an",
            Description = "Da chinh sua va chuyen sang hidden de kiem tra luong trang thai moi.",
            JobType = 1,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Ba Dinh",
            Status = (int)JobPostingStatus.Hidden
        });

        var updatedJob = await fixture.Db.JobPostings.FirstAsync(j => j.Id == jobId);
        Assert.Equal((int)JobPostingStatus.Hidden, updatedJob.Status);
        Assert.Equal((int)JobPostingModerationStatus.Approved, updatedJob.ModerationStatus);
        Assert.NotNull(updatedJob.ClosedAt);

        var myJobs = await fixture.JobService.getMyJobs(fixture.FreeParentProfileId);
        var historyItem = Assert.Single(myJobs, j => j.Id == jobId);
        Assert.Equal((int)JobPostingStatus.Hidden, historyItem.Status);
        Assert.Equal((int)JobPostingModerationStatus.Approved, historyItem.ModerationStatus);
    }

    [Fact]
    public async Task CreateJob_SetsPendingModeration_AndCreatesPendingNotification()
    {
        await using var fixture = await TestFixture.create();

        var jobId = await fixture.JobService.createJob(fixture.FreeParentProfileId, new CreateJobPostingRequest
        {
            Title = "Bai dang moi cho moderator duyet",
            Description = "Bai nay duoc tao de kiem tra notification cho trang thai pending.",
            JobType = 1,
            NumberOfChildren = 1,
            SalaryNegotiable = true,
            City = "Ha Noi",
            District = "Dong Da",
            Status = (int)JobPostingStatus.Public
        });

        var createdJob = await fixture.Db.JobPostings.FirstAsync(j => j.Id == jobId);
        Assert.Equal((int)JobPostingModerationStatus.Pending, createdJob.ModerationStatus);

        var notification = await fixture.Db.Notifications
            .Where(n => n.UserId == fixture.FreeParentUserId && n.RelatedEntityId == jobId)
            .SingleAsync();

        Assert.Equal(NotificationTypes.JobPostingPending, notification.Type);
        Assert.Contains("cho moderator duyet", notification.Title, StringComparison.OrdinalIgnoreCase);
        Assert.False(notification.IsRead);
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

    [Fact]
    public async Task CreatePayment_CreatesPendingTransaction_AndCheckoutSession()
    {
        await using var fixture = await TestFixture.create();

        var plan = (await fixture.SubscriptionService.getPlans()).Single(p => p.Code == "PLUS");

        var session = await fixture.SubscriptionService.createPayment(
            fixture.FreeParentUserId,
            new CreateSubscriptionPaymentRequest { SubscriptionPlanId = plan.Id });

        Assert.NotEqual(Guid.Empty, session.TransactionId);
        Assert.Equal(plan.Price, session.Amount);
        Assert.False(string.IsNullOrWhiteSpace(session.CheckoutUrl));

        var transaction = await fixture.Db.Transactions.SingleAsync(t => t.Id == session.TransactionId);
        Assert.Equal(1, transaction.Status);
        Assert.Equal(1, transaction.Type);
        Assert.Equal(session.OrderCode.ToString(), transaction.PaymentGatewayTransactionId);
        Assert.StartsWith("NM PLUS ", transaction.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VietQrWebhook_MatchedPayment_ActivatesSubscription_AndCreatesNotification()
    {
        await using var fixture = await TestFixture.create();
        var plan = (await fixture.SubscriptionService.getPlans()).Single(p => p.Code == "PLUS");

        var session = await fixture.SubscriptionService.createPayment(
            fixture.FreeParentUserId,
            new CreateSubscriptionPaymentRequest { SubscriptionPlanId = plan.Id });

        var processed = await fixture.SubscriptionService.handleVietQrWebhook(new VietQrWebhookRequest
        {
            Data =
            [
                new VietQrWebhookPaymentData
                {
                    OrderCode = session.OrderCode,
                    Amount = plan.Price,
                    Code = "00"
                }
            ]
        });

        Assert.Equal(1, processed);

        var transaction = await fixture.Db.Transactions.SingleAsync(t => t.Id == session.TransactionId);
        Assert.Equal(2, transaction.Status);
        Assert.NotNull(transaction.CompletedAt);

        var subscription = await fixture.Db.UserSubscriptions.SingleAsync(s =>
            s.PaymentTransactionId == transaction.Id && !s.IsDeleted);
        Assert.Equal(1, subscription.Status);

        Assert.True(await fixture.Db.Notifications.AnyAsync(n =>
            n.UserId == fixture.FreeParentUserId &&
            n.Type == NotificationTypes.SubscriptionPurchased &&
            n.RelatedEntityId == subscription.Id));
    }

    [Fact]
    public async Task VietQrWebhook_MismatchedAmount_MarksTransactionFailed_WithoutSubscription()
    {
        await using var fixture = await TestFixture.create();
        var plan = (await fixture.SubscriptionService.getPlans()).Single(p => p.Code == "PLUS");

        var session = await fixture.SubscriptionService.createPayment(
            fixture.FreeParentUserId,
            new CreateSubscriptionPaymentRequest { SubscriptionPlanId = plan.Id });

        var processed = await fixture.SubscriptionService.handleVietQrWebhook(new VietQrWebhookRequest
        {
            Data =
            [
                new VietQrWebhookPaymentData
                {
                    OrderCode = session.OrderCode,
                    Amount = plan.Price + 10000,
                    Code = "00"
                }
            ]
        });

        Assert.Equal(0, processed);

        var transaction = await fixture.Db.Transactions.SingleAsync(t => t.Id == session.TransactionId);
        Assert.Equal(3, transaction.Status);

        Assert.False(await fixture.Db.UserSubscriptions.AnyAsync(s =>
            s.PaymentTransactionId == transaction.Id && !s.IsDeleted));
        Assert.False(await fixture.Db.Notifications.AnyAsync(n =>
            n.UserId == fixture.FreeParentUserId &&
            n.Type == NotificationTypes.SubscriptionPurchased));
    }

    [Fact]
    public async Task VietQrWebhook_DuplicateFailedCallback_DoesNotOverrideSuccess()
    {
        await using var fixture = await TestFixture.create();
        var plan = (await fixture.SubscriptionService.getPlans()).Single(p => p.Code == "PLUS");

        var session = await fixture.SubscriptionService.createPayment(
            fixture.FreeParentUserId,
            new CreateSubscriptionPaymentRequest { SubscriptionPlanId = plan.Id });

        var firstProcessed = await fixture.SubscriptionService.handleVietQrWebhook(new VietQrWebhookRequest
        {
            Data =
            [
                new VietQrWebhookPaymentData
                {
                    OrderCode = session.OrderCode,
                    Amount = plan.Price,
                    Code = "00"
                }
            ]
        });

        var secondProcessed = await fixture.SubscriptionService.handleVietQrWebhook(new VietQrWebhookRequest
        {
            Data =
            [
                new VietQrWebhookPaymentData
                {
                    OrderCode = session.OrderCode,
                    Amount = plan.Price + 10,
                    Code = "99"
                }
            ]
        });

        Assert.Equal(1, firstProcessed);
        Assert.Equal(0, secondProcessed);

        var transaction = await fixture.Db.Transactions.SingleAsync(t => t.Id == session.TransactionId);
        Assert.Equal(2, transaction.Status);

        Assert.Equal(1, await fixture.Db.UserSubscriptions.CountAsync(s =>
            s.PaymentTransactionId == transaction.Id && !s.IsDeleted));
    }

    [Fact]
    public async Task VietQrWebhook_InvalidTransactionDescription_DoesNotThrow_AndMarksFailed()
    {
        await using var fixture = await TestFixture.create();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = fixture.FreeParentUserId,
            Amount = 299000m,
            PaymentGatewayTransactionId = "998877665",
            Status = 1,
            Description = "INVALID CONTENT",
            Type = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = fixture.FreeParentUserId
        };

        fixture.Db.Transactions.Add(transaction);
        await fixture.Db.SaveChangesAsync();

        var ex = await Record.ExceptionAsync(() => fixture.SubscriptionService.handleVietQrWebhook(new VietQrWebhookRequest
        {
            Data =
            [
                new VietQrWebhookPaymentData
                {
                    OrderCode = 998877665,
                    Amount = 299000m,
                    Code = "00"
                }
            ]
        }));

        Assert.Null(ex);

        var updated = await fixture.Db.Transactions.SingleAsync(t => t.Id == transaction.Id);
        Assert.Equal(3, updated.Status);
        Assert.False(await fixture.Db.UserSubscriptions.AnyAsync(s => s.PaymentTransactionId == transaction.Id && !s.IsDeleted));
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
            var vnPayService = new VnPayService(Options.Create(new VnPayOptions
            {
                TmnCode = "TESTTMN",
                HashSecret = "TEST_HASH_SECRET",
                PaymentUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                ReturnUrl = "https://api.example.test/api/subscriptions/vnpay/return",
                SuccessUrl = "https://ui.example.test/Subscription/PaymentResult?transactionId={transactionId}",
                CancelUrl = "https://ui.example.test/Subscription/PaymentResult?cancelled=true&transactionId={transactionId}"
            }));
            var subscriptionService = new SubscriptionService(subscriptionRepo, notificationService, vnPayService);
            var geo = new GeocodingService(new FakeHttpClientFactory());
            var jobService = new JobService(jobRepo, favoriteRepo, geo, subscriptionService, notificationService);

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
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.Contains("paymentRequests", StringComparison.OrdinalIgnoreCase) == true)
            {
                var orderCode = 123456789;
                var amount = 0;
                var description = "NM PLUS 123456789";
                var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("orderCode", out var orderCodeProp))
                        orderCode = orderCodeProp.GetInt32();
                    if (json.RootElement.TryGetProperty("amount", out var amountProp))
                        amount = amountProp.GetInt32();
                    if (json.RootElement.TryGetProperty("description", out var descProp))
                        description = descProp.GetString() ?? description;
                }

                var payload = JsonSerializer.Serialize(new
                {
                    code = "00",
                    desc = "success",
                    data = new
                    {
                        id = $"PAY-{orderCode}",
                        amount,
                        description,
                        orderCode,
                        status = "PENDING",
                        checkoutUrl = $"https://pay.example.test/checkout/{orderCode}"
                    }
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            };
        }
    }
}
