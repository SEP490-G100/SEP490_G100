using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Controllers;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Xunit;

namespace Nanny_BackEnd.Tests;

public class SearchJobApplicationReviewTests
{
    [Fact]
    public async Task ParentCanViewApplicationsByJob()
    {
        await using var fixture = await TestFixture.Create();
        var controller = fixture.CreateControllerAsParent();

        var result = await controller.GetJobApplicationsForParent(fixture.JobPostingId);

        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));

        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(fixture.JobPostingId, data.GetProperty("job").GetProperty("id").GetGuid());
        Assert.Equal(1, data.GetProperty("totalApplications").GetInt32());
        Assert.Equal(1, data.GetProperty("pendingApplications").GetInt32());
    }

    [Fact]
    public async Task ParentAcceptRequest_UpdatesStatusAndNotifiesNanny()
    {
        await using var fixture = await TestFixture.Create();
        var controller = fixture.CreateControllerAsParent();

        var result = await controller.ReviewJobApplication(
            fixture.ApplicationId,
            new SearchController.ReviewJobApplicationRequest { Action = 1 });

        Assert.IsType<OkObjectResult>(result);

        var updated = await fixture.Db.JobApplications.FirstAsync(a => a.Id == fixture.ApplicationId);
        Assert.Equal(1, updated.Status);
        Assert.NotNull(updated.ReviewedAt);
        Assert.Null(updated.RejectionReason);

        var notification = await fixture.Db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync(n =>
                n.UserId == fixture.NannyUserId &&
                n.Type == NotificationTypes.JobApplicationApproved &&
                n.RelatedEntityId == fixture.ApplicationId);

        Assert.NotNull(notification);
    }

    [Fact]
    public async Task ParentRejectWithoutReason_ReturnsBadRequest()
    {
        await using var fixture = await TestFixture.Create();
        var controller = fixture.CreateControllerAsParent();

        var result = await controller.ReviewJobApplication(
            fixture.ApplicationId,
            new SearchController.ReviewJobApplicationRequest { Action = 2, RejectionReason = " " });

        Assert.IsType<BadRequestObjectResult>(result);

        var application = await fixture.Db.JobApplications.FirstAsync(a => a.Id == fixture.ApplicationId);
        Assert.Equal(0, application.Status);
        Assert.Null(application.ReviewedAt);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private TestFixture(
            Sep490NannyDbContext db,
            NotificationService notificationService,
            Guid parentUserId,
            Guid nannyUserId,
            Guid jobPostingId,
            Guid applicationId)
        {
            Db = db;
            NotificationService = notificationService;
            ParentUserId = parentUserId;
            NannyUserId = nannyUserId;
            JobPostingId = jobPostingId;
            ApplicationId = applicationId;
        }

        public Sep490NannyDbContext Db { get; }
        public NotificationService NotificationService { get; }
        public Guid ParentUserId { get; }
        public Guid NannyUserId { get; }
        public Guid JobPostingId { get; }
        public Guid ApplicationId { get; }

        public static async Task<TestFixture> Create()
        {
            var options = new DbContextOptionsBuilder<Sep490NannyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var db = new Sep490NannyDbContext(options);
            var subscriptionRepo = new SubscriptionRepository(db);
            var notificationService = new NotificationService(subscriptionRepo);

            var now = DateTime.UtcNow;
            var parentUserId = Guid.NewGuid();
            var parentProfileId = Guid.NewGuid();
            var nannyUserId = Guid.NewGuid();
            var nannyProfileId = Guid.NewGuid();
            var jobPostingId = Guid.NewGuid();
            var applicationId = Guid.NewGuid();

            db.Users.AddRange(
                new User
                {
                    Id = parentUserId,
                    Email = "parent@test.vn",
                    FirstName = "Parent",
                    LastName = "One",
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
                    Email = "nanny@test.vn",
                    FirstName = "Nanny",
                    LastName = "One",
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
                YearsOfExperience = 5,
                SalaryType = 1,
                TotalReviews = 0,
                ProfileCompleteness = 80,
                IsDeleted = false,
                CreatedAt = now
            });

            db.JobPostings.Add(new JobPosting
            {
                Id = jobPostingId,
                ParentProfileId = parentProfileId,
                Title = "Can nanny cham be",
                Description = "Tim nanny cham be 2 tuoi",
                JobType = 1,
                SalaryType = 1,
                SalaryNegotiable = false,
                Status = 1,
                ModerationStatus = 2,
                IsDeleted = false,
                CreatedAt = now
            });

            db.JobApplications.Add(new JobApplication
            {
                Id = applicationId,
                JobPostingId = jobPostingId,
                NannyProfileId = nannyProfileId,
                Status = 0,
                IsDeleted = false,
                CreatedAt = now,
                CreatedBy = nannyUserId
            });

            await db.SaveChangesAsync();

            return new TestFixture(db, notificationService, parentUserId, nannyUserId, jobPostingId, applicationId);
        }

        public SearchController CreateControllerAsParent()
        {
            var controller = new SearchController(null!, Db, NotificationService);
            var claimsIdentity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, ParentUserId.ToString()),
                new Claim(ClaimTypes.Role, "Parent")
            ], "test-auth");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(claimsIdentity)
                }
            };

            return controller;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
