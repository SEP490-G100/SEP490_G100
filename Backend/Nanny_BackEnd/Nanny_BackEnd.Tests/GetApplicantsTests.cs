using Microsoft.AspNetCore.SignalR;
using Moq;
using FluentAssertions;
using Nanny_BackEnd.Hubs;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

public class GetApplicantsTests
{
    private readonly Mock<IHiringRepository> _mockRepo;
    private readonly HiringService          _sut;

    public GetApplicantsTests()
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

    // ── Helper ────────────────────────────────────────────────────────────
    private static JobPosting MakeJob(Guid parentUserId) => new()
    {
        Id            = Guid.NewGuid(),
        ParentProfile = new ParentProfile { UserId = parentUserId }
    };

    // ── TC1: Job không tồn tại → KeyNotFoundException ─────────────────────
    [Fact]
    public async Task JobNotFound()
    {
        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(It.IsAny<Guid>()))
                 .ReturnsAsync((JobPosting?)null);

        var act = () => _sut.GetApplicantsAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>()
                 .WithMessage("*Khong tim thay bai dang*");
    }

    // ── TC2: Parent không phải chủ job → UnauthorizedAccessException ──────
    [Fact]
    public async Task UnauthorizedOwner()
    {
        var job = MakeJob(parentUserId: Guid.NewGuid());
        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(job.Id)).ReturnsAsync(job);

        var act = () => _sut.GetApplicantsAsync(job.Id, Guid.NewGuid());  // caller khác owner

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("*quyen xem*");
    }

    // ── TC3: Có 2 đơn ứng tuyển → trả về đúng số lượng, mapping Status ──
    [Fact]
    public async Task WithApplications_MapsCorrectly()
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
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockRepo.Setup(r => r.GetJobPostingByIdAsync(job.Id)).ReturnsAsync(job);
        _mockRepo.Setup(r => r.GetApplicantsByJobPostingIdAsync(job.Id)).ReturnsAsync(applications);

        var result = await _sut.GetApplicantsAsync(job.Id, parentUserId);

        result.Should().HaveCount(2);
        result[0].Status.Should().Be(0);
        result[1].Status.Should().Be(1);
        result.Select(r => r.NannyName).Should().NotContainNulls();
    }
}
