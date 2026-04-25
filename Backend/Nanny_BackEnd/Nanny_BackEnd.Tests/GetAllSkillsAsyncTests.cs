using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.OnboardingController.GetSkills"/> →
/// <see cref="OnboardingService.GetAllSkillsAsync"/>.
/// </summary>
public class GetAllSkillsAsyncTests
{
    private readonly Mock<IUserRepository> _mockUser;
    private readonly Mock<IParentRepository> _mockParent;
    private readonly Mock<IChildRepository> _mockChild;
    private readonly Mock<INannyProfileRepository> _mockNannyProfile;
    private readonly Mock<INannySkillRepository> _mockNannySkill;
    private readonly Mock<INannyAvailabilityRepository> _mockNannyAvail;
    private readonly Mock<IJobRepository> _mockJob;
    private readonly OnboardingService _sut;

    public GetAllSkillsAsyncTests()
    {
        _mockUser = new Mock<IUserRepository>();
        _mockParent = new Mock<IParentRepository>();
        _mockChild = new Mock<IChildRepository>();
        _mockNannyProfile = new Mock<INannyProfileRepository>();
        _mockNannySkill = new Mock<INannySkillRepository>();
        _mockNannyAvail = new Mock<INannyAvailabilityRepository>();
        _mockJob = new Mock<IJobRepository>();
        _sut = new OnboardingService(
            _mockUser.Object,
            _mockParent.Object,
            _mockChild.Object,
            _mockNannyProfile.Object,
            _mockNannySkill.Object,
            _mockNannyAvail.Object,
            _mockJob.Object,
            NullLogger<OnboardingService>.Instance);
    }


    [Fact]
    public async Task NoSkills_EmptyList()
    {
        _mockJob.Setup(j => j.getActiveSkills()).ReturnsAsync(new List<Skill>());

        var r = await _sut.GetAllSkillsAsync();

        Assert.NotNull(r);
        Assert.Empty(r);
        _mockJob.Verify(j => j.getActiveSkills(), Times.Once);
    }

    [Fact]
    public async Task ReturnsMappedSkills_InOrder()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _mockJob.Setup(j => j.getActiveSkills()).ReturnsAsync(new List<Skill>
        {
            new()
            {
                Id = a,
                Name = "Sơ cấp",
                Category = "Y tế"
            },
            new()
            {
                Id = b,
                Name = "CPR",
            }
        });

        var r = await _sut.GetAllSkillsAsync();

        Assert.Equal(2, r.Count);
        Assert.Equal(a, r[0].SkillId);
        Assert.Equal("Sơ cấp", r[0].SkillName);
        Assert.Equal("Y tế", r[0].Category);
        Assert.Equal(b, r[1].SkillId);
        Assert.Equal("CPR", r[1].SkillName);
        _mockJob.Verify(j => j.getActiveSkills(), Times.Once);
    }
}
