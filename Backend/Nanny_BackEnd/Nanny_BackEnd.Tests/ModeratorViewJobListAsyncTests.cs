using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ModeratorJobController.ModeratorViewJobList"/> →
/// <see cref="ModeratorJobService.ModeratorViewJobListAsync"/>.
/// </summary>
public class ModeratorViewJobListAsyncTests
{
    private readonly Mock<IModeratorJobRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly ModeratorJobService _sut;

    public ModeratorViewJobListAsyncTests()
    {
        _mockRepo = new Mock<IModeratorJobRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new ModeratorJobService(_mockRepo.Object, _mockNotif.Object);
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

    // Condition: gọi repository với đúng tham số lọc/phân trang.
    [Fact]
    public async Task ForwardsArgumentsToRepository()
    {
        _mockRepo.Setup(r => r.ModeratorViewJobListAsync(1, 2, "key", 3, 15))
            .ReturnsAsync((new List<JobPosting>(), 0));

        await _sut.ModeratorViewJobListAsync(1, 2, "key", 3, 15);

        _mockRepo.Verify(
            r => r.ModeratorViewJobListAsync(1, 2, "key", 3, 15),
            Times.Once);
    }

    // Condition: repo trả về rỗng.
    [Fact]
    public async Task EmptyResult_ReturnsEmptyItemsAndTotal()
    {
        _mockRepo.Setup(r => r.ModeratorViewJobListAsync(null, null, null, 1, 10))
            .ReturnsAsync((new List<JobPosting>(), 0));

        var (items, total) = await _sut.ModeratorViewJobListAsync(null, null, null, 1, 10);

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    // Condition: repo trả về bản ghi — map sang <see cref="Nanny_BackEnd.DTOs.Search.SearchJobResponse"/> và giữ TotalCount.
    [Fact]
    public async Task MapsItemsAndPreservesTotalCount()
    {
        var jobId = Guid.NewGuid();
        var job = MakeJob(jobId, "Viec test");
        _mockRepo.Setup(r => r.ModeratorViewJobListAsync(0, 1, null, 2, 5))
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
