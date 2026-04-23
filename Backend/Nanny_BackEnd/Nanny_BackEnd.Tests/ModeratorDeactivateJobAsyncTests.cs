using Moq;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ModeratorJobController.ModeratorDeactivateJob"/> (or equivalent) →
/// <see cref="ModeratorJobService.ModeratorDeactivateJobAsync"/>.
/// </summary>
public class ModeratorDeactivateJobAsyncTests
{
    private const string NotFoundMessage = "Không tìm thấy tin đăng hoặc tin đã bị xóa.";

    private readonly Mock<IModeratorJobRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly ModeratorJobService _sut;

    public ModeratorDeactivateJobAsyncTests()
    {
        _mockRepo = new Mock<IModeratorJobRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new ModeratorJobService(_mockRepo.Object, _mockNotif.Object);
    }

    private static JobPosting MakeJob(
        Guid jobId,
        string title = "Công việc",
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

    // Condition: job không tồn tại.
    [Fact]
    public async Task NotFound_ThrowsKeyNotFound()
    {
        var jobId = Guid.NewGuid();
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ModeratorDeactivateJobAsync(jobId, Guid.NewGuid()));

        Assert.Equal(NotFoundMessage, ex.Message);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // Condition: IsDeleted = true — thoát sớm, không lưu, không thông báo.
    [Fact]
    public async Task AlreadyDeleted_ReturnsWithoutSave()
    {
        var jobId = Guid.NewGuid();
        var job = MakeJob(jobId, isDeleted: true);
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);

        await _sut.ModeratorDeactivateJobAsync(jobId, Guid.NewGuid());

        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
        _mockNotif.Verify(
            n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    // Condition: vô hiệu hóa — cập nhật entity và SaveChanges.
    [Fact]
    public async Task Success_SetsFlagsAndSaves()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var job = MakeJob(jobId);
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        await _sut.ModeratorDeactivateJobAsync(jobId, modId);

        Assert.True(job.IsDeleted);
        Assert.Equal((int)JobPostingStatus.Hidden, job.Status);
        Assert.Equal(modId, job.UpdatedBy);
        Assert.NotNull(job.ClosedAt);
        Assert.NotNull(job.UpdatedAt);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    // Condition: có ParentProfile — thông báo từ chối.
    [Fact]
    public async Task WithParent_SendsNotification()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var parentUser = Guid.NewGuid();
        var job = MakeJob(jobId, "Việc X", parent: new ParentProfile { UserId = parentUser });
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockNotif
            .Setup(n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        await _sut.ModeratorDeactivateJobAsync(jobId, modId);

        _mockNotif.Verify(
            n => n.createNotification(
                parentUser,
                "Bài đăng đã bị vô hiệu hóa",
                "Bài đăng \"Việc X\" đã bị điều hành viên vô hiệu hóa.",
                NotificationTypes.JobPostingRejected,
                jobId,
                "JobPosting",
                modId),
            Times.Once);
    }

    // Condition: không có parent — không gọi createNotification.
    [Fact]
    public async Task WithoutParent_SkipsNotification()
    {
        var jobId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var job = MakeJob(jobId, parent: null);
        _mockRepo.Setup(r => r.ModeratorViewJobDetailAsync(jobId)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        await _sut.ModeratorDeactivateJobAsync(jobId, modId);

        _mockNotif.Verify(
            n => n.createNotification(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Never);
    }
}
