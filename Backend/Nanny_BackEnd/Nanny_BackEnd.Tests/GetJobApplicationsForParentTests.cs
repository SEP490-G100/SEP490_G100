using Moq;
using FluentAssertions;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

public class GetJobApplicationsForParentTests
{
    private readonly Mock<IJobApplicationRepository> _mockAppRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<ISubscriptionService>     _mockSubSvc;
    private readonly SearchService                 _sut;

    private readonly Guid _parentUserId    = Guid.NewGuid();
    private readonly Guid _parentProfileId = Guid.NewGuid();
    private readonly Guid _jobId          = Guid.NewGuid();

    public GetJobApplicationsForParentTests()
    {
        _mockAppRepo = new Mock<IJobApplicationRepository>();
        _mockNotif   = new Mock<INotificationService>();
        _mockSubSvc  = new Mock<ISubscriptionService>();
        _sut         = new SearchService(_mockAppRepo.Object, _mockNotif.Object, _mockSubSvc.Object);
    }

    [Fact]
    public async Task InvalidStatusFilter_ReturnsError()
    {
        var (err, _, _) = await _sut.GetJobApplicationsForParentAsync(
            _parentUserId, _jobId, status: 5);

        err.Should().Be(GetParentJobApplicationsFailure.InvalidStatusFilter);
    }

    [Fact]
    public async Task NotParent_ReturnsNotParent()
    {
        _mockAppRepo.Setup(r => r.GetParentProfileIdByUserIdAsync(_parentUserId))
            .ReturnsAsync((Guid?)null);

        var (err, _, data) = await _sut.GetJobApplicationsForParentAsync(
            _parentUserId, _jobId, null);

        err.Should().Be(GetParentJobApplicationsFailure.NotParent);
        data.Should().BeNull();
    }

    [Fact]
    public async Task JobNotFound_ReturnsJobNotFound()
    {
        _mockAppRepo.Setup(r => r.GetParentProfileIdByUserIdAsync(_parentUserId))
            .ReturnsAsync(_parentProfileId);
        _mockAppRepo.Setup(r => r.GetJobPostingForParentAsync(_jobId, _parentProfileId))
            .ReturnsAsync((JobPosting?)null);

        var (err, _, data) = await _sut.GetJobApplicationsForParentAsync(
            _parentUserId, _jobId, null);

        err.Should().Be(GetParentJobApplicationsFailure.JobNotFound);
        data.Should().BeNull();
    }

    [Fact]
    public async Task Success_ReturnsApplications()
    {
        var job = new JobPosting
        {
            Id                = _jobId,
            ParentProfileId   = _parentProfileId,
            Title             = "Tuyen nanny",
            Status            = 1,
            ModerationStatus  = 1,
            City              = "HN",
            District          = "Q1",
            Location          = "A",
            CreatedAt         = DateTime.UtcNow,
            PublishedAt       = DateTime.UtcNow,
            ExpiresAt         = DateTime.UtcNow.AddDays(7),
            IsDeleted         = false
        };
        var nannyU = new User
        {
            Id = Guid.NewGuid(), FirstName = "N", LastName = "1", City = "HN", District = "Q1", PhoneNumber = "1"
        };
        _mockAppRepo.Setup(r => r.GetParentProfileIdByUserIdAsync(_parentUserId))
            .ReturnsAsync(_parentProfileId);
        _mockAppRepo.Setup(r => r.GetJobPostingForParentAsync(_jobId, _parentProfileId))
            .ReturnsAsync(job);
        _mockAppRepo.Setup(r => r.GetApplicationsByJobPostingAsync(_jobId, null))
            .ReturnsAsync(new List<JobApplication>
            {
                new()
                {
                    Id             = Guid.NewGuid(),
                    JobPostingId   = _jobId,
                    NannyProfileId = Guid.NewGuid(),
                    Status         = 0,
                    CreatedAt      = DateTime.UtcNow,
                    NannyProfile = new NannyProfile
                    {
                        Id   = Guid.NewGuid(),
                        User = nannyU,
                        UserId = nannyU.Id,
                        YearsOfExperience = 2,
                        ExpectedSalaryMin  = 1,
                        ExpectedSalaryMax  = 2
                    }
                }
            });

        var (err, errMsg, data) = await _sut.GetJobApplicationsForParentAsync(
            _parentUserId, _jobId, null);

        err.Should().BeNull();
        data.Should().NotBeNull();
        data!.Job.Title.Should().Be("Tuyen nanny");
        data.Applications.Should().HaveCount(1);
        data.Applications[0].Nanny.FullName.Should().Be("N 1");
    }
}
