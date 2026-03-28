using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nanny_BackEnd.Controllers;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Xunit;

namespace Nanny_BackEnd.Tests;

public class NannyApplyQuotaTests
{
    [Fact]
    public async Task FreeNanny_IsLimitedToTwoApplicationsPerMonth()
    {
        await using var fixture = await Fixture.Create();
        var controller = fixture.CreateControllerAsNanny();

        Assert.IsType<OkObjectResult>(await controller.ApplyJob(fixture.JobIds[0]));
        Assert.IsType<OkObjectResult>(await controller.ApplyJob(fixture.JobIds[1]));

        var third = await controller.ApplyJob(fixture.JobIds[2]);
        var bad = Assert.IsType<BadRequestObjectResult>(third);
        var payload = JsonSerializer.Serialize(bad.Value);

        Assert.Contains("gioi han 2", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NannyPlus_IsLimitedToThreeApplicationsPerMonth()
    {
        await using var fixture = await Fixture.Create();
        await fixture.SubscribeNannyPlan("NANNY_PLUS");
        var controller = fixture.CreateControllerAsNanny();

        Assert.IsType<OkObjectResult>(await controller.ApplyJob(fixture.JobIds[0]));
        Assert.IsType<OkObjectResult>(await controller.ApplyJob(fixture.JobIds[1]));
        Assert.IsType<OkObjectResult>(await controller.ApplyJob(fixture.JobIds[2]));

        var fourth = await controller.ApplyJob(fixture.JobIds[3]);
        var bad = Assert.IsType<BadRequestObjectResult>(fourth);
        var payload = JsonSerializer.Serialize(bad.Value);

        Assert.Contains("gioi han 3", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NannyPro_IsLimitedToFiveApplicationsPerMonth()
    {
        await using var fixture = await Fixture.CreateWithJobCount(6);
        await fixture.SubscribeNannyPlan("NANNY_PRO");
        var controller = fixture.CreateControllerAsNanny();

        for (var i = 0; i < 5; i++)
            Assert.IsType<OkObjectResult>(await controller.ApplyJob(fixture.JobIds[i]));

        var sixth = await controller.ApplyJob(fixture.JobIds[5]);
        var bad = Assert.IsType<BadRequestObjectResult>(sixth);
        var payload = JsonSerializer.Serialize(bad.Value);

        Assert.Contains("gioi han 5", payload, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            Sep490NannyDbContext db,
            NotificationService notificationService,
            SubscriptionService subscriptionService,
            Guid nannyUserId,
            Guid nannyProfileId,
            List<Guid> jobIds)
        {
            Db = db;
            NotificationService = notificationService;
            SubscriptionService = subscriptionService;
            NannyUserId = nannyUserId;
            NannyProfileId = nannyProfileId;
            JobIds = jobIds;
        }

        public Sep490NannyDbContext Db { get; }
        public NotificationService NotificationService { get; }
        public SubscriptionService SubscriptionService { get; }
        public Guid NannyUserId { get; }
        public Guid NannyProfileId { get; }
        public List<Guid> JobIds { get; }

        public static async Task<Fixture> Create()
            => await CreateWithJobCount(4);

        public static async Task<Fixture> CreateWithJobCount(int jobCount)
        {
            var options = new DbContextOptionsBuilder<Sep490NannyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new Sep490NannyDbContext(options);
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

            var now = DateTime.UtcNow;
            var parentUserId = Guid.NewGuid();
            var parentProfileId = Guid.NewGuid();
            var nannyUserId = Guid.NewGuid();
            var nannyProfileId = Guid.NewGuid();

            db.Users.AddRange(
                new User
                {
                    Id = parentUserId,
                    Email = "parent.apply@test.vn",
                    FirstName = "Parent",
                    LastName = "Apply",
                    Status = 1,
                    AuthProvider = 0,
                    EmailConfirmed = true,
                    PhoneConfirmed = false,
                    IsDeleted = false,
                    CreatedAt = now
                },
                new User
                {
                    Id = nannyUserId,
                    Email = "nanny.apply@test.vn",
                    FirstName = "Nanny",
                    LastName = "Apply",
                    Status = 1,
                    AuthProvider = 0,
                    EmailConfirmed = true,
                    PhoneConfirmed = false,
                    IsDeleted = false,
                    CreatedAt = now
                });

            db.ParentProfiles.Add(new ParentProfile
            {
                Id = parentProfileId,
                UserId = parentUserId,
                IsDeleted = false,
                CreatedAt = now
            });

            db.NannyProfiles.Add(new NannyProfile
            {
                Id = nannyProfileId,
                UserId = nannyUserId,
                VerificationStatus = 2,
                YearsOfExperience = 4,
                SalaryType = 1,
                TotalReviews = 0,
                ProfileCompleteness = 90,
                IsDeleted = false,
                CreatedAt = now
            });

            var jobIds = new List<Guid>();
            for (var i = 0; i < jobCount; i++)
            {
                var jobId = Guid.NewGuid();
                jobIds.Add(jobId);
                db.JobPostings.Add(new JobPosting
                {
                    Id = jobId,
                    ParentProfileId = parentProfileId,
                    Title = $"Can nanny {i}",
                    Description = $"Bai dang so {i}",
                    JobType = 1,
                    SalaryType = 1,
                    SalaryNegotiable = true,
                    Status = (int)JobPostingStatus.Public,
                    ModerationStatus = (int)JobPostingModerationStatus.Approved,
                    IsDeleted = false,
                    CreatedAt = now.AddMinutes(-i)
                });
            }

            await db.SaveChangesAsync();

            return new Fixture(db, notificationService, subscriptionService, nannyUserId, nannyProfileId, jobIds);
        }

        public async Task SubscribeNannyPlan(string planCode)
        {
            var plans = await SubscriptionService.getPlans();
            var plan = plans.Single(p => p.Code == planCode);
            await SubscriptionService.subscribe(NannyUserId, new SubscribeRequest
            {
                SubscriptionPlanId = plan.Id,
                PaymentGatewayTransactionId = $"{planCode}-{Guid.NewGuid()}"
            });
        }

        public SearchController CreateControllerAsNanny()
        {
            var controller = new SearchController(null!, Db, NotificationService, SubscriptionService);
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, NannyUserId.ToString()),
                new Claim(ClaimTypes.Role, "Nanny")
            ], "test-auth");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            return controller;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
