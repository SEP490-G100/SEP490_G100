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
/// <see cref="Nanny_BackEnd.Controllers.ModeratorJobController.ModeratorReviewJob"/> (or review endpoint) →
/// <see cref="ModeratorJobService.ModeratorReviewJobAsync"/>.
/// </summary>
public class ModeratorReviewJobAsyncTests
{
    private const string NotFoundMessage = "Không tìm thấy tin đăng hoặc tin đã bị xóa.";

    private readonly Mock<IModeratorJobRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly ModeratorJobService _sut;

    public ModeratorReviewJobAsyncTests()
    {
        _mockRepo = new Mock<IModeratorJobRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new ModeratorJobService(_mockRepo.Object, _mockNotif.Object);
    }

    private static JobPosting BaseJob(
        Guid jobId,
        int status,
        string title = "Tin mẫu",
        ParentProfile? parent = null) => new()
    {
        Id = jobId,
        ParentProfileId = Guid.NewGuid(),
        ParentProfile = parent!,
        Title = title,
        Description = "D",
        Status = status,
        CreatedAt = DateTime.UtcNow,
        JobRequirements = new List<JobRequirement>(),
        JobScheduleRequirements = new List<JobScheduleRequirement>(),
        JobApplications = new List<JobApplication>()
    };

    // Condition: job không tồn tại.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var jobId = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync((JobPosting?)null);
        var req = new ModerateJobPostingRequest { Action = (int)JobPostingModerationStatus.Approved };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ModeratorReviewJobAsync(jobId, Guid.NewGuid(), req));

        Assert.Equal(NotFoundMessage, ex.Message);
        _mockRepo.Verify(r => r.SaveModeratedJobAsync(It.IsAny<JobPosting>()), Times.Never);
    }

    // Condition: duyệt, tin ở trạng thái công khai (Public) → PublishedAt, ClosedAt = null.
    [Fact]
    public async Task Approved_Public_Job_SetsPublishedAt()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var parentUser = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Public, parent: new ParentProfile { UserId = parentUser });
        JobPosting? saved = null;
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo
            .Setup(r => r.SaveModeratedJobAsync(It.IsAny<JobPosting>()))
            .Callback<JobPosting>(j => saved = j)
            .Returns(Task.CompletedTask);
        _mockNotif
            .Setup(n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, modId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Approved
        });

        Assert.NotNull(saved);
        Assert.Equal((int)JobPostingModerationStatus.Approved, saved!.ModerationStatus);
        Assert.Equal(modId, saved.ModeratedBy);
        Assert.NotNull(saved.PublishedAt);
        Assert.Null(saved.ClosedAt);
        _mockRepo.Verify(r => r.SaveModeratedJobAsync(It.IsAny<JobPosting>()), Times.Once);
    }

    // Condition: duyệt, tin ẩn (Hidden) → ClosedAt, PublishedAt = null.
    [Fact]
    public async Task Approved_Hidden_Job_SetsClosedAt()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Hidden, parent: null);
        JobPosting? saved = null;
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo
            .Setup(r => r.SaveModeratedJobAsync(It.IsAny<JobPosting>()))
            .Callback<JobPosting>(j => saved = j)
            .Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, modId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Approved
        });

        Assert.NotNull(saved);
        Assert.Null(saved!.PublishedAt);
        Assert.NotNull(saved.ClosedAt);
    }

    // Condition: từ chối → PublishedAt = null, ClosedAt có, ghi chú trim.
    [Fact]
    public async Task Rejected_ClearsPublished_SetsClosedAndTrimsNote()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Public, parent: null);
        JobPosting? saved = null;
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo
            .Setup(r => r.SaveModeratedJobAsync(It.IsAny<JobPosting>()))
            .Callback<JobPosting>(j => saved = j)
            .Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, modId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Rejected,
            Note = "  lỗi  "
        });

        Assert.NotNull(saved);
        Assert.Equal((int)JobPostingModerationStatus.Rejected, saved!.ModerationStatus);
        Assert.Equal("lỗi", saved.ModerationNote);
        Assert.Null(saved.PublishedAt);
        Assert.NotNull(saved.ClosedAt);
    }

    // Condition: có ParentProfile — gửi thông báo khi từ chối, type rejected.
    [Fact]
    public async Task WithParent_SendsRejectionNotification_WithNoteInContent()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var parentUser = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Public, "Việc ABC", new ParentProfile { UserId = parentUser });
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.SaveModeratedJobAsync(It.IsAny<JobPosting>())).Returns(Task.CompletedTask);
        _mockNotif
            .Setup(n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, modId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Rejected,
            Note = "Sai mô tả"
        });

        _mockNotif.Verify(
            n => n.createNotification(
                parentUser,
                "Bài đăng của bạn đã bị từ chối",
                "Bài đăng \"Việc ABC\" đã bị từ chối. Lý do: Sai mô tả",
                NotificationTypes.JobPostingRejected,
                jobId,
                "JobPosting",
                modId),
            Times.Once);
    }

    // Condition: không có Parent — không tạo notification.
    [Fact]
    public async Task WithoutParent_SkipsNotification()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var job = BaseJob(jobId, (int)JobPostingStatus.Public, parent: null);
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.SaveModeratedJobAsync(It.IsAny<JobPosting>())).Returns(Task.CompletedTask);

        await _sut.ModeratorReviewJobAsync(jobId, modId, new ModerateJobPostingRequest
        {
            Action = (int)JobPostingModerationStatus.Approved
        });

        _mockNotif.Verify(
            n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Never);
    }
}
