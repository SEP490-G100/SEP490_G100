using Moq;
using FluentAssertions;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>Unit tests cho <see cref="SearchService.GetMyApplicationsAsync"/> — mock <see cref="IJobApplicationRepository"/>.</summary>
public class MyApplicationsTests
{
    private readonly Mock<IJobApplicationRepository> _mockAppRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<ISubscriptionService>     _mockSubSvc;
    private readonly SearchService                 _sut;

    private readonly Guid _nannyUserId   = Guid.NewGuid();
    private readonly Guid _nannyProfileId = Guid.NewGuid();

    public MyApplicationsTests()
    {
        _mockAppRepo = new Mock<IJobApplicationRepository>();
        _mockNotif   = new Mock<INotificationService>();
        _mockSubSvc  = new Mock<ISubscriptionService>();
        _sut         = new SearchService(_mockAppRepo.Object, _mockNotif.Object, _mockSubSvc.Object);
    }

    [Fact]
    public async Task NannyProfileNotFound_ReturnsNull()
    {
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId))
            .ReturnsAsync((NannyProfile?)null);

        var result = await _sut.GetMyApplicationsAsync(_nannyUserId, 1, 20);

        result.Should().BeNull();
    }

    [Fact]
    public async Task PageSizeClamped()
    {
        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId))
            .ReturnsAsync(new NannyProfile
            {
                Id = _nannyProfileId, UserId = _nannyUserId, IsDeleted = false,
                User = new User { Id = _nannyUserId, FirstName = "N", LastName = "1" }
            });
        _mockAppRepo.Setup(r => r.GetPagedApplicationsForNannyAsync(_nannyProfileId, 0, 50))
            .ReturnsAsync((new List<JobApplication>(), 0));

        var result = await _sut.GetMyApplicationsAsync(_nannyUserId, page: 1, pageSize: 100);

        result.Should().NotBeNull();
        result!.PageSize.Should().Be(50);
        _mockAppRepo.Verify(r => r.GetPagedApplicationsForNannyAsync(_nannyProfileId, 0, 50), Times.Once);
    }

    [Fact]
    public async Task WithApplications_ReturnsCorrectTotalAndCanWithdraw()
    {
        var parentU = new User
        {
            Id = Guid.NewGuid(), FirstName = "P", LastName = "A", CreatedAt = DateTime.UtcNow
        };
        var ppId = Guid.NewGuid();
        var job = new JobPosting
        {
            Id                = Guid.NewGuid(),
            ParentProfileId   = ppId,
            Title             = "Tim bao mau",
            IsDeleted         = false,
            ParentProfile = new ParentProfile
            {
                Id     = ppId,
                UserId = parentU.Id,
                User   = parentU
            }
        };

        var items = new List<JobApplication>
        {
            new()
            {
                Id             = Guid.NewGuid(),
                JobPostingId   = job.Id,
                NannyProfileId = _nannyProfileId,
                Status         = 0,
                IsDeleted      = false,
                CreatedAt      = DateTime.UtcNow,
                JobPosting     = job
            },
            new()
            {
                Id             = Guid.NewGuid(),
                JobPostingId   = job.Id,
                NannyProfileId = _nannyProfileId,
                Status         = 1,
                IsDeleted      = false,
                CreatedAt      = DateTime.UtcNow,
                JobPosting     = job
            }
        };

        _mockAppRepo.Setup(r => r.GetNannyProfileWithUserAsync(_nannyUserId))
            .ReturnsAsync(new NannyProfile
            {
                Id = _nannyProfileId, UserId = _nannyUserId, IsDeleted = false,
                User = new User { Id = _nannyUserId, FirstName = "N", LastName = "1" }
            });
        _mockAppRepo.Setup(r => r.GetPagedApplicationsForNannyAsync(_nannyProfileId, 0, 20))
            .ReturnsAsync((items, 2));

        var result = await _sut.GetMyApplicationsAsync(_nannyUserId, 1, 20);

        result.Should().NotBeNull();
        result!.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.Status == 0 && i.CanWithdraw);
        result.Items.Should().Contain(i => i.Status == 1 && !i.CanWithdraw);
    }
}
