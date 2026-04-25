using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nanny_BackEnd.DTOs.JobPosting;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// Tests for:
/// JobPostingController.ModeratorReviewJob -> JobService.ModeratorReviewJobAsync
/// </summary>
public class ReviewJobAsyncTests
{
    private readonly Mock<IJobRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly JobService _sut;

    public ReviewJobAsyncTests()
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

    private static JobPosting BaseJob(
        Guid jobId,
        int status,
        string title = "Tin mau",
        ParentProfile? parent = null) => new()
    {
        Id = jobId,
        ParentProfileId = Guid.NewGuid(),
        ParentProfile = parent ?? null!,
        Title = title,
        Description = "Mo ta",
        Status = status,
        ModerationStatus = (int)JobPostingModerationStatus.Pending,
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

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ModeratorReviewJobAsync(jobId, Guid.NewGuid(), new ModerateJobPostingRequest
            {
                Action = (int)JobPostingModerationStatus.Approved
            }));

        Assert.NotNull(ex.Message);
        _mockRepo.Verify(r => r.updateJobPosting(It.IsAny<JobPosting>()), Times.Never);
    }

    // Condition: approved + job status Public -> PublishedAt set, ClosedAt null.
    [Fact]
    public async Task AlreadyModerated_ThrowsInvalidOperation()
    {
        var jobId = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Public, parent: new ParentProfile { UserId = Guid.NewGuid() });
        job.ModerationStatus = (int)JobPostingModerationStatus.Approved;

        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ModeratorReviewJobAsync(jobId, Guid.NewGuid(), new ModerateJobPostingRequest
            {
                Action = (int)JobPostingModerationStatus.Rejected,
                Note = "lý do"
            }));

        Assert.Contains("không thể xử lý lại", ex.Message.ToLowerInvariant());
        _mockRepo.Verify(r => r.updateJobPosting(It.IsAny<JobPosting>()), Times.Never);
        _mockNotif.Verify(n => n.createNotification(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task Approved_PublicJob_SetsPublishedAt()
    {
        var jobId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();

        var job = BaseJob(
            jobId,
            (int)JobPostingStatus.Public,
            parent: new ParentProfile { UserId = parentUserId });

        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.updateJobPosting(It.IsAny<JobPosting>())).Returns(Task.CompletedTask);
        _mockNotif.Setup(n => n.createNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, moderatorId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Approved
        });

        Assert.Equal((int)JobPostingModerationStatus.Approved, job.ModerationStatus);
        Assert.Equal(moderatorId, job.ModeratedBy);
        Assert.NotNull(job.ModeratedAt);
        Assert.NotNull(job.PublishedAt);
        Assert.Null(job.ClosedAt);

        _mockRepo.Verify(r => r.updateJobPosting(job), Times.Once);
        _mockNotif.Verify(n => n.createNotification(
                parentUserId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.JobPostingApproved,
                jobId,
                "JobPosting",
                moderatorId),
            Times.Once);
    }

    // Condition: approved + job status Hidden -> ClosedAt set, PublishedAt null.
    [Fact]
    public async Task Approved_HiddenJob_SetsClosedAt()
    {
        var jobId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Hidden, parent: null);

        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.updateJobPosting(It.IsAny<JobPosting>())).Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, moderatorId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Approved
        });

        Assert.Equal((int)JobPostingModerationStatus.Approved, job.ModerationStatus);
        Assert.Null(job.PublishedAt);
        Assert.NotNull(job.ClosedAt);
        _mockNotif.Verify(n => n.createNotification(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>()), Times.Never);
    }

    // Condition: rejected -> PublishedAt null, ClosedAt set, note trimmed.
    [Fact]
    public async Task Rejected_SetsClosedAt_AndTrimsNote()
    {
        var jobId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Public, parent: null);

        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.updateJobPosting(It.IsAny<JobPosting>())).Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, moderatorId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Rejected,
            Note = "  vi pham noi dung  "
        });

        Assert.Equal((int)JobPostingModerationStatus.Rejected, job.ModerationStatus);
        Assert.Equal("vi pham noi dung", job.ModerationNote);
        Assert.Null(job.PublishedAt);
        Assert.NotNull(job.ClosedAt);
    }

    // Condition: rejected + has parent profile -> send rejected notification with reason.
    [Fact]
    public async Task Rejected_WithParent_SendsNotification()
    {
        var jobId = Guid.NewGuid();
        var moderatorId = Guid.NewGuid();
        var parentUserId = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Public, "Viec ABC", new ParentProfile { UserId = parentUserId });

        _mockRepo.Setup(r => r.viewDetailPosting(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.updateJobPosting(It.IsAny<JobPosting>())).Returns(Task.CompletedTask);
        _mockNotif.Setup(n => n.createNotification(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, moderatorId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Rejected,
            Note = "Sai mo ta"
        });

        _mockNotif.Verify(n => n.createNotification(
                parentUserId,
                It.IsAny<string>(),
                It.Is<string>(content => !string.IsNullOrWhiteSpace(content)),
                NotificationTypes.JobPostingRejected,
                jobId,
                "JobPosting",
                moderatorId),
            Times.Once);
    }

    // Condition: request action is invalid -> throw and skip repository update.
    [Fact]
    public async Task InvalidAction_ThrowsInvalidOperation()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ModeratorReviewJobAsync(Guid.NewGuid(), Guid.NewGuid(), new ModerateJobPostingRequest
            {
                Action = 99
            }));

        Assert.NotNull(ex.Message);
        _mockRepo.Verify(r => r.viewDetailPosting(It.IsAny<Guid>()), Times.Never);
        _mockRepo.Verify(r => r.updateJobPosting(It.IsAny<JobPosting>()), Times.Never);
    }
}
