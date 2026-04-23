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
/// JobPostingController.ModeratorViewJobDetail -> JobService.ModeratorViewJobDetailAsync
/// </summary>
public class ViewJobDetailAsyncTests
{
    private const string NotFoundMessage = "Khong tim thay job posting.";

    private readonly Mock<IJobRepository> _mockRepo;
    private readonly JobService _sut;

    public ViewJobDetailAsyncTests()
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

    private static JobPosting MakeJob(Guid id) => new()
    {
        Id = id,
        ParentProfileId = Guid.NewGuid(),
        Title = "Chi tiet job",
        Description = "Mo ta",
        Status = 1,
        ModerationStatus = 0,
        CreatedAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        JobRequirements = new List<JobRequirement>(),
        JobScheduleRequirements = new List<JobScheduleRequirement>(),
        JobApplications = new List<JobApplication> { new(), new() }
    };

    // Condition: repository returns null.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(id)).ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.ModeratorViewJobDetailAsync(id));

        Assert.Equal(NotFoundMessage, ex.Message);
    }

    // Condition: forwards exact jobId to repository.
    [Fact]
    public async Task ForwardsJobIdToRepository()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(id)).ReturnsAsync(MakeJob(id));

        await _sut.ModeratorViewJobDetailAsync(id);

        _mockRepo.Verify(r => r.ModeratorViewJobDetailAsync(id), Times.Once);
    }

    // Condition: returns mapped detail DTO.
    [Fact]
    public async Task ReturnsMappedDetail()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(id)).ReturnsAsync(MakeJob(id));

        var detail = await _sut.ModeratorViewJobDetailAsync(id);

        Assert.Equal(id, detail.Id);
        Assert.Equal("Chi tiet job", detail.Title);
        Assert.Equal("Mo ta", detail.Description);
        Assert.Equal(2, detail.ApplicationCount);
        Assert.Equal(Guid.Empty, detail.ParentUserId);
        Assert.Null(detail.SubscriptionPlanCode);
        Assert.Equal(SubscriptionBenefitResponse.FreeParent.FeaturedBadge, detail.FeaturedBadge);
        Assert.Equal(SubscriptionBenefitResponse.FreeParent.SearchPriority, detail.SearchPriority);
    }
}
