using Moq;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

public class ApproveApplicantAsyncTests
{
    private readonly Mock<IHiringRepository> _mockRepo = new();
    private readonly HiringService _sut;

    public ApproveApplicantAsyncTests()
    {
        var mockCommSvc = new Mock<ICommunicationService>();
        _sut = new HiringService(_mockRepo.Object, mockCommSvc.Object);
    }

    [Fact]
    public async Task PendingApplication_ApprovesAndSendsNotificationToNanny()
    {
        var parentUserId = Guid.NewGuid();
        var nannyUserId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        var application = new JobApplication
        {
            Id = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            NannyProfileId = Guid.NewGuid(),
            Status = 0,
            JobPosting = new JobPosting
            {
                Id = jobPostingId,
                Title = "Tim bao mau",
                ParentProfile = new ParentProfile { UserId = parentUserId }
            },
            NannyProfile = new NannyProfile { UserId = nannyUserId }
        };

        _mockRepo.Setup(r => r.GetJobApplicationByIdAsync(application.Id))
            .ReturnsAsync(application);

        await _sut.ApproveApplicantAsync(jobPostingId, application.Id, parentUserId);

        Assert.Equal(1, application.Status);
        Assert.NotNull(application.ReviewedAt);
        Assert.Equal(parentUserId, application.UpdatedBy);
        _mockRepo.Verify(r => r.AddNotification(It.Is<Notification>(n =>
            n.UserId == nannyUserId &&
            n.Type == NotificationTypes.JobApplicationApproved &&
            n.RelatedEntityId == application.Id &&
            n.RelatedEntityType == "JobApplication" &&
            n.CreatedBy == parentUserId)), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
