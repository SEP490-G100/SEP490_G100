using Microsoft.AspNetCore.SignalR;
using Moq;
using Nanny_BackEnd.Hubs;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// </summary>
public class GetApplicantsAsyncTests
{
    private readonly Mock<IHiringRepository> _mockRepo;
    private readonly HiringService             _sut;

    public GetApplicantsAsyncTests()
    {
        _mockRepo = new Mock<IHiringRepository>();

        var mockSubRepo  = new Mock<ISubscriptionRepository>();
        var mockUserRepo = new Mock<IUserRepository>();
        var mockNotif    = new Mock<NotificationService>(mockSubRepo.Object, mockUserRepo.Object);
        var mockCommRepo = new Mock<ICommunicationRepository>();
        var mockRptRepo  = new Mock<IReportRepository>();
        var mockRptSvc   = new Mock<ReportService>(mockRptRepo.Object, mockUserRepo.Object, mockNotif.Object);
        var mockChatHub  = new Mock<IHubContext<ChatHub>>();
        var mockCommSvc  = new Mock<CommunicationService>(
            mockCommRepo.Object, mockNotif.Object, mockUserRepo.Object,
            mockRptSvc.Object, mockChatHub.Object);

        _sut = new HiringService(_mockRepo.Object, mockCommSvc.Object);
    }

    private static JobPosting MakeJob(Guid parentUserId) => new()
    {
        Id            = Guid.NewGuid(),
        ParentProfile = new ParentProfile { UserId = parentUserId }
    };

    // -- Abnormal -------------------------------------------------------
    // Condition: GetJobPostingById trả null.
    [Fact]
    public async Task Abnormal_JobNotFound_Throws()
    {
        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(It.IsAny<Guid>()))
                 .ReturnsAsync((JobPosting?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.GetApplicantsAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    // Confirmation: UnauthorizedAccessException.
    [Fact]
    public async Task Abnormal_NotJobOwner_Throws()
    {
        var job = MakeJob(parentUserId: Guid.NewGuid());
        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(job.Id)).ReturnsAsync(job);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.GetApplicantsAsync(job.Id, Guid.NewGuid()));

    }

    // -- Normal ---------------------------------------------------------
    [Fact]
    public async Task Normal_TwoApplicants_Mapped()
    {
        var parentUserId = Guid.NewGuid();
        var job          = MakeJob(parentUserId);

        var applications = new List<JobApplication>
        {
            new()
            {
                Id             = Guid.NewGuid(),
                NannyProfileId = Guid.NewGuid(),
                NannyProfile   = new NannyProfile
                {
                    Id   = Guid.NewGuid(),
                    User = new User { FirstName = "Nguyen", LastName = "An" }
                },
                Status    = 0,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id             = Guid.NewGuid(),
                NannyProfileId = Guid.NewGuid(),
                NannyProfile   = new NannyProfile
                {
                    Id   = Guid.NewGuid(),
                    User = new User { FirstName = "Tran", LastName = "Binh" }
                },
                Status    = 1,
                RejectionReason = "Khong phu hop lich trinh",
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(job.Id)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.GetApplicantsByJobPostingIdAsync(job.Id)).ReturnsAsync(applications);

        var result = await _sut.GetApplicantsAsync(job.Id, parentUserId);

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].Status);
        Assert.Equal(1, result[1].Status);
        Assert.Equal("Khong phu hop lich trinh", result[1].RejectionReason);
        Assert.Equal(new[] { "Nguyen An", "Tran Binh" },
            result.Select(r => r.NannyName).ToArray());
    }

    // -- Boundary --------------------------------------------------------
    [Fact]
    public async Task Boundary_NoApplications_ReturnsEmptyList()
    {
        var parentUserId = Guid.NewGuid();
        var job          = MakeJob(parentUserId);
        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(job.Id)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.GetApplicantsByJobPostingIdAsync(job.Id))
                 .ReturnsAsync(new List<JobApplication>());

        var result = await _sut.GetApplicantsAsync(job.Id, parentUserId);

        Assert.Empty(result);
    }

    // Confirmation: NannyName = fallback từ GetDisplayName.
    [Fact]
    public async Task Boundary_NullNannyUser_UsesFallbackDisplayName()
    {
        var parentUserId = Guid.NewGuid();
        var job          = MakeJob(parentUserId);
        var uid          = Guid.NewGuid();
        var applications = new List<JobApplication>
        {
            new()
            {
                Id              = Guid.NewGuid(),
                NannyProfileId  = uid,
                NannyProfile = new NannyProfile
                {
                    Id     = uid,
                    UserId = uid,
                    User   = null!
                },
                Status    = 0,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(job.Id)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.GetApplicantsByJobPostingIdAsync(job.Id)).ReturnsAsync(applications);

        var result = await _sut.GetApplicantsAsync(job.Id, parentUserId);

        Assert.Single(result);
        Assert.Equal("Người dùng", result[0].NannyName);
        Assert.NotEqual(Guid.Empty, result[0].NannyUserId);
    }
}
