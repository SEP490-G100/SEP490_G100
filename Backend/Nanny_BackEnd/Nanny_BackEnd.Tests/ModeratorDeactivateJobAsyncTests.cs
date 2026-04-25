using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Tests for:
/// JobPostingController.ModeratorDeactivateJob -> JobService.ModeratorDeactivateJobAsync
/// </summary>
public class ModeratorDeactivateJobAsyncTests
{
    private readonly Mock<IJobRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly JobService _sut;

    public ModeratorDeactivateJobAsyncTests()
    {
        _mockRepo = new Mock<IJobRepository>();
        _mockNotif = new Mock<INotificationService>();

        var mockFavoriteRepo = new Mock<IFavoriteRepository>();
        var mockGeo = new Mock<IGeocodingService>();
        var mockSubscriptionService = new Mock<ISubscriptionService>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockLogger = new Mock<ILogger<JobService>>();

        _sut = new JobService(
            _mockRepo.Object,
            mockFavoriteRepo.Object,
            mockGeo.Object,
            mockSubscriptionService.Object,
            _mockNotif.Object,
            mockScopeFactory.Object,
            mockLogger.Object);
    }

    private static JobPosting MakeJob(
        Guid jobId,
        string title = "Cong viec",
        bool isDeleted = false,
        ParentProfile? parent = null) => new()
    {
        Id = jobId,
        ParentProfileId = Guid.NewGuid(),
        ParentProfile = parent!,
        Title = title,
        Description = "D",
        Status = (int)JobPostingStatus.Public,
        IsDeleted = isDeleted,
        CreatedAt = DateTime.UtcNow,
        JobRequirements = new List<JobRequirement>(),
        JobScheduleRequirements = new List<JobScheduleRequirement>(),
        JobApplications = new List<JobApplication>()
    };

    // Condition: job not found.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var jobId = Guid.NewGuid();
        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync((JobPosting?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ModeratorDeactivateJobAsync(jobId, Guid.NewGuid()));

        _mockRepo.Verify(r => r.saveChanges(), Times.Never);
    }

    // Condition: IsDeleted = true -> early return, no save, no notification.
    [Fact]
    public async Task AlreadyDeleted_ReturnsWithoutSave()
    {
        var jobId = Guid.NewGuid();
        var job = MakeJob(jobId, isDeleted: true);
        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);

        await _sut.ModeratorDeactivateJobAsync(jobId, Guid.NewGuid());

        _mockRepo.Verify(r => r.saveChanges(), Times.Never);
        _mockNotif.Verify(
            n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    // Condition: deactivate -> update entity and save.
    [Fact]
    public async Task Success_SetsFlagsAndSaves()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var job = MakeJob(jobId);
        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);

        await _sut.ModeratorDeactivateJobAsync(jobId, modId);

        Assert.True(job.IsDeleted);
        Assert.Equal((int)JobPostingStatus.Hidden, job.Status);
        Assert.Equal(modId, job.UpdatedBy);
        Assert.NotNull(job.ClosedAt);
        Assert.NotNull(job.UpdatedAt);
        _mockRepo.Verify(r => r.saveChanges(), Times.Once);
    }

    // Condition: has parent profile -> send notification.
    [Fact]
    public async Task WithParent_SendsNotification()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var parentUser = Guid.NewGuid();
        var job = MakeJob(jobId, "Viec X", parent: new ParentProfile { UserId = parentUser });
        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);
        _mockNotif
            .Setup(n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        await _sut.ModeratorDeactivateJobAsync(jobId, modId);

        _mockNotif.Verify(
            n => n.createNotification(
                parentUser,
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("Viec X")),
                NotificationTypes.JobPostingRejected,
                jobId,
                "JobPosting",
                modId),
            Times.Once);
    }

    // Condition: no parent profile -> do not send notification.
    [Fact]
    public async Task WithoutParent_SkipsNotification()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var job = MakeJob(jobId, parent: null);
        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.saveChanges()).Returns(Task.CompletedTask);

        await _sut.ModeratorDeactivateJobAsync(jobId, modId);

        _mockNotif.Verify(
            n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Never);
    }
}
