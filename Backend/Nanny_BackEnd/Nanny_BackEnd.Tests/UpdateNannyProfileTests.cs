using Moq;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.OnboardingController.UpdateNannyProfile"/> →
/// <see cref="OnboardingService.UpdateNannyProfileAsync"/>.
/// </summary>
public class UpdateNannyProfileTests
{
    private readonly Mock<IUserRepository> _mockUser;
    private readonly Mock<IParentRepository> _mockParent;
    private readonly Mock<IChildRepository> _mockChild;
    private readonly Mock<INannyProfileRepository> _mockNannyProfile;
    private readonly Mock<INannySkillRepository> _mockNannySkill;
    private readonly Mock<INannyAvailabilityRepository> _mockNannyAvail;
    private readonly Mock<IJobRepository> _mockJob;
    private readonly OnboardingService _sut;

    public UpdateNannyProfileTests()
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
            _mockJob.Object);
    }

    private static UpdateNannyProfileRequest ValidRequest() => new()
    {
        Bio = "X",
        YearsOfExperience = 4,
        EducationLevel = EducationLevel.Bachelor,
        ExpectedSalaryMin = 9_000_000m,
        ExpectedSalaryMax = 20_000_000m,
        MaxTravelDistance = 20
    };

        // Condition: lương từ ngoài khoảng quy định.
    [Fact]
    public async Task SalaryMinOutOfRange_Throws()
    {
        var id = Guid.NewGuid();
        var req = new UpdateNannyProfileRequest
        {
            ExpectedSalaryMin = 1_000_000m,
            ExpectedSalaryMax = 9_000_000m
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateNannyProfileAsync(id, req));

        Assert.Equal(
            $"Muc luong toi thieu phải trong khoảng {SalaryValidationRules.SalaryRangeText}.",
            ex.Message);
    }

    // Condition: mức tối thiểu lớn hơn mức tối đa.
    [Fact]
    public async Task SalaryMinGreaterThanMax_Throws()
    {
        var id = Guid.NewGuid();
        var req = new UpdateNannyProfileRequest
        {
            ExpectedSalaryMin = 30_000_000m,
            ExpectedSalaryMax = 9_000_000m
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateNannyProfileAsync(id, req));

        Assert.Equal("Muc luong toi thieu không được lớn hơn Muc luong toi da.", ex.Message);
    }

    // Condition: chưa có NannyProfile.
    // Confirmation: tạo bản ghi, Add, Save, trả về cùng thực thể (đã gán field).
    [Fact]
    public async Task NoExistingProfile_CreatesAndPersists()
    {
        var id = Guid.NewGuid();
        _mockNannyProfile.Setup(n => n.FindByUserIdAsync(id)).ReturnsAsync((NannyProfile?)null);
        _mockNannyProfile.Setup(n => n.SaveChangesAsync()).Returns(Task.CompletedTask);

        var profile = await _sut.UpdateNannyProfileAsync(id, ValidRequest());

        _mockNannyProfile.Verify(n => n.Add(It.Is<NannyProfile>(p => p.UserId == id && p.IsDeleted == false)), Times.Once);
        _mockNannyProfile.Verify(n => n.SaveChangesAsync(), Times.Once);
        Assert.Equal("X", profile.Bio);
        Assert.Equal(4, profile.YearsOfExperience);
        Assert.Equal((int)EducationLevel.Bachelor, profile.EducationLevel);
        Assert.Equal(9_000_000m, profile.ExpectedSalaryMin);
        Assert.Equal(20_000_000m, profile.ExpectedSalaryMax);
        Assert.Equal(20, profile.MaxTravelDistance);
        Assert.Equal((int)VerificationStatus.NotSubmitted, profile.VerificationStatus);
    }

    // Condition: đã có NannyProfile.
    [Fact]
    public async Task ExistingProfile_UpdatesInPlace()
    {
        var id = Guid.NewGuid();
        var npId = Guid.NewGuid();
        var existing = new NannyProfile
        {
            Id = npId,
            UserId = id,
            Bio = "old",
            YearsOfExperience = 1,
            EducationLevel = (int)EducationLevel.HighSchool,
            ExpectedSalaryMin = 10_000_000m,
            ExpectedSalaryMax = 10_000_000m,
            MaxTravelDistance = 5,
            VerificationStatus = (int)VerificationStatus.Pending
        };
        _mockNannyProfile.Setup(n => n.FindByUserIdAsync(id)).ReturnsAsync(existing);
        _mockNannyProfile.Setup(n => n.SaveChangesAsync()).Returns(Task.CompletedTask);

        var req = ValidRequest();
        var profile = await _sut.UpdateNannyProfileAsync(id, req);

        Assert.Same(existing, profile);
        Assert.Equal(npId, profile.Id);
        Assert.Equal("X", profile.Bio);
        Assert.Equal(4, profile.YearsOfExperience);
        Assert.Equal((int)EducationLevel.Bachelor, profile.EducationLevel);
        _mockNannyProfile.Verify(n => n.Add(It.IsAny<NannyProfile>()), Times.Never);
        _mockNannyProfile.Verify(n => n.SaveChangesAsync(), Times.Once);
    }
}
