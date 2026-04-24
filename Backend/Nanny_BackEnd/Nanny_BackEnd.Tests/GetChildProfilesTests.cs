using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ProfileController.GetChildProfiles"/> →
/// <see cref="ProfileService.GetChildProfilesAsync"/>.
/// </summary>
public class GetChildProfilesTests
{
    private const string OnlyParentMessage = "Chỉ người dùng có vị trí Parent mới có thể xem.";

    private readonly Mock<IUserRepository> _mockUser;
    private readonly Mock<IParentRepository> _mockParent;
    private readonly Mock<IChildRepository> _mockChild;
    private readonly Mock<INannyProfileRepository> _mockNannyProfile;
    private readonly Mock<INannySkillRepository> _mockNannySkill;
    private readonly Mock<INannyCertificateRepository> _mockNannyCert;
    private readonly Mock<INannyAvailabilityRepository> _mockNannyAvail;
    private readonly Mock<IVerificationRequestRepository> _mockVerification;
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<IGeocodingService> _mockGeo;
    private readonly Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory> _mockScope;
    private readonly ProfileService _sut;

    public GetChildProfilesTests()
    {
        _mockUser = new Mock<IUserRepository>();
        _mockParent = new Mock<IParentRepository>();
        _mockChild = new Mock<IChildRepository>();
        _mockNannyProfile = new Mock<INannyProfileRepository>();
        _mockNannySkill = new Mock<INannySkillRepository>();
        _mockNannyCert = new Mock<INannyCertificateRepository>();
        _mockNannyAvail = new Mock<INannyAvailabilityRepository>();
        _mockVerification = new Mock<IVerificationRequestRepository>();
        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockGeo = new Mock<IGeocodingService>();
        _mockScope = new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        _sut = new ProfileService(
            _mockUser.Object,
            _mockParent.Object,
            _mockChild.Object,
            _mockNannyProfile.Object,
            _mockNannySkill.Object,
            _mockNannyCert.Object,
            _mockNannyAvail.Object,
            _mockVerification.Object,
            _mockEnv.Object,
            _mockGeo.Object,
            _mockScope.Object,
            NullLogger<ProfileService>.Instance);
    }

    // Condition: user không có role parent.
    // Confirmation: UnauthorizedAccessException, không truy cập ParentProfile/Child.
    [Fact]
    public async Task NotParent_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.GetChildProfilesAsync(id));

        Assert.Equal(OnlyParentMessage, ex.Message);
        _mockParent.Verify(p => p.FindByUserIdAsync(It.IsAny<Guid>()), Times.Never);
        _mockChild.Verify(c => c.GetByParentProfileIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: role parent, chưa có ParentProfile.
    // Confirmation: danh sách rỗng, không gọi GetByParentProfileId.
    [Fact]
    public async Task ParentRole_NoParentProfile_ReturnsEmpty()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(id)).ReturnsAsync((ParentProfile?)null);

        var list = await _sut.GetChildProfilesAsync(id);

        Assert.NotNull(list);
        Assert.Empty(list);
        _mockChild.Verify(c => c.GetByParentProfileIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: phụ huynh có hồ sơ và nhiều hồ sơ con.
    // Confirmation: DTO ánh xạ đúng từng bản ghi.
    [Fact]
    public async Task Parent_MapsChildProfiles()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        _mockUser.Setup(u => u.GetRolesAsync(userId)).ReturnsAsync(new List<string> { "Parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(userId)).ReturnsAsync(new ParentProfile { Id = parentId, UserId = userId });
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(parentId))
            .ReturnsAsync(new List<ChildProfile>
            {
                new()
                {
                    Id = c1,
                    ParentProfileId = parentId,
                    SpecialNeeds = "A",
                    Notes = "N1",
                    Characteristic = "C1",
                    ChildAgeGroup = 2,
                    CreatedAt = t0
                },
                new()
                {
                    Id = c2,
                    ParentProfileId = parentId,
                    SpecialNeeds = "B",
                    Notes = "N2",
                    Characteristic = "C2",
                    ChildAgeGroup = 3,
                    CreatedAt = t1
                }
            });

        var list = await _sut.GetChildProfilesAsync(userId);

        Assert.Equal(2, list.Count);
        Assert.Equal(c1, list[0].Id);
        Assert.Equal(parentId, list[0].ParentProfileId);
        Assert.Equal("A", list[0].SpecialNeeds);
        Assert.Equal("N1", list[0].Notes);
        Assert.Equal("C1", list[0].Characteristic);
        Assert.Equal((byte)2, list[0].ChildAgeGroup);
        Assert.Equal(t0, list[0].CreatedAt);
        _mockChild.Verify(c => c.GetByParentProfileIdAsync(parentId), Times.Once);
    }
}
