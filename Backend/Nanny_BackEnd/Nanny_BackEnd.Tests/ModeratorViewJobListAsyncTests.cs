using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Tests for:
/// JobPostingController.ModeratorViewJobList -> JobService.ModeratorViewJobListAsync
/// </summary>
public class ModeratorViewJobListAsyncTests
{
    private readonly Mock<IJobRepository> _mockRepo;
    private readonly JobService _sut;

    public ModeratorViewJobListAsyncTests()
    {
        _mockRepo = new Mock<IJobRepository>();

        var mockFavoriteRepo = new Mock<IFavoriteRepository>();
        var mockGeo = new Mock<IGeocodingService>();
        var mockSubscriptionService = new Mock<ISubscriptionService>();
        var mockNotif = new Mock<INotificationService>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockLogger = new Mock<ILogger<JobService>>();

        _sut = new JobService(
            _mockRepo.Object,
            mockFavoriteRepo.Object,
            mockGeo.Object,
            mockSubscriptionService.Object,
            mockNotif.Object,
            mockScopeFactory.Object,
            mockLogger.Object);
    }

    private static JobPosting MakeJob(Guid id, string title) => new()
    {
        Id = id,
        ParentProfileId = Guid.NewGuid(),
        Title = title,
        Description = "D",
        CreatedAt = DateTime.UtcNow,
        JobRequirements = new List<JobRequirement>(),
        JobScheduleRequirements = new List<JobScheduleRequirement>(),
        JobApplications = new List<JobApplication>()
    };

    // Condition: forwards filter/paging arguments to repository.
    [Fact]
    public async Task ForwardsArgumentsToRepository()
    {
        _mockRepo.Setup(r => r.GetModeratorJobPostingsAsync(1, 2, "key", 3, 15))
            .ReturnsAsync((new List<JobPosting>(), 0));

        await _sut.ModeratorViewJobListAsync(1, 2, "key", 3, 15);

        _mockRepo.Verify(
            r => r.GetModeratorJobPostingsAsync(1, 2, "key", 3, 15),
            Times.Once);
    }

    // Condition: repository returns empty.
    [Fact]
    public async Task EmptyResult_ReturnsEmptyItemsAndTotal()
    {
        _mockRepo.Setup(r => r.GetModeratorJobPostingsAsync(null, null, null, 1, 10))
            .ReturnsAsync((new List<JobPosting>(), 0));

        var (items, total) = await _sut.ModeratorViewJobListAsync(null, null, null, 1, 10);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    // Condition: maps records and preserves TotalCount.
    [Fact]
    public async Task MapsItemsAndPreservesTotalCount()
    {
        var jobId = Guid.NewGuid();
        var job = MakeJob(jobId, "Viec test");
        _mockRepo.Setup(r => r.GetModeratorJobPostingsAsync(0, 1, null, 2, 5))
            .ReturnsAsync((new List<JobPosting> { job }, 42));

        var (items, total) = await _sut.ModeratorViewJobListAsync(0, 1, null, 2, 5);

        Assert.Equal(42, total);
        Assert.Single(items);
        Assert.Equal(jobId, items[0].Id);
        Assert.Equal("Viec test", items[0].Title);
        Assert.Equal("D", items[0].Description);
        Assert.Null(items[0].SubscriptionPlanCode);
        Assert.Equal(SubscriptionBenefitResponse.FreeParent.FeaturedBadge, items[0].FeaturedBadge);
        Assert.Equal(SubscriptionBenefitResponse.FreeParent.SearchPriority, items[0].SearchPriority);
    }
}
