using Moq;
using Nanny_BackEnd.DTOs.Subscription;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ModeratorJobController.ModeratorViewJobDetail"/> →
/// <see cref="ModeratorJobService.ModeratorViewJobDetailAsync"/>.
/// </summary>
public class ModeratorViewJobDetailAsyncTests
{
    private const string NotFoundMessage = "Không tìm thấy tin đăng hoặc tin đã bị xóa.";

    private readonly Mock<IModeratorJobRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly ModeratorJobService _sut;

    public ModeratorViewJobDetailAsyncTests()
    {
        _mockRepo = new Mock<IModeratorJobRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new ModeratorJobService(_mockRepo.Object, _mockNotif.Object);
    }

    private static JobPosting MakeJob(Guid id) => new()
    {
        Id = id,
        ParentProfileId = Guid.NewGuid(),
        Title = "Chi tiết job",
        Description = "Mô tả",
        Status = 1,
        ModerationStatus = 0,
        CreatedAt = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        JobRequirements = new List<JobRequirement>(),
        JobScheduleRequirements = new List<JobScheduleRequirement>(),
        JobApplications = new List<JobApplication> { new(), new() }
    };

    // Condition: repository trả về null.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(id)).ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.ModeratorViewJobDetailAsync(id));

        Assert.Equal(NotFoundMessage, ex.Message);
    }

    // Condition: gọi đúng jobId.
    [Fact]
    public async Task ForwardsJobIdToRepository()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(id)).ReturnsAsync(MakeJob(id));

        await _sut.ModeratorViewJobDetailAsync(id);

        _mockRepo.Verify(r => r.ModeratorViewJobDetailAsync(id), Times.Once);
    }

    // Condition: trả về DTO chi tiết từ mapToDetail.
    [Fact]
    public async Task ReturnsMappedDetail()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(id)).ReturnsAsync(MakeJob(id));

        var detail = await _sut.ModeratorViewJobDetailAsync(id);

        Assert.Equal(id, detail.Id);
        Assert.Equal("Chi tiết job", detail.Title);
        Assert.Equal("Mô tả", detail.Description);
        Assert.Equal(2, detail.ApplicationCount);
        Assert.Equal(Guid.Empty, detail.ParentUserId);
        Assert.Null(detail.SubscriptionPlanCode);
        Assert.Equal(SubscriptionBenefitResponse.FreeParent.FeaturedBadge, detail.FeaturedBadge);
        Assert.Equal(SubscriptionBenefitResponse.FreeParent.SearchPriority, detail.SearchPriority);
    }
}
