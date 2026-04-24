using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ParentOnboardingController.UpdateParentProfile"/> →
/// <see cref="ProfileService.UpdateParentOnboardingProfileAsync"/>.
/// </summary>
public class UpdateParentProfileTests
{
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

    public UpdateParentProfileTests()
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

    // Condition: NumberOfChildren &lt; 1 khi có giá trị.
    [Fact]
    public async Task NumberOfChildrenBelowOne_Throws()
    {
        var id = Guid.NewGuid();
        var req = new UpdateParentProfileRequest { NumberOfChildren = 0, FamilyDescription = "F" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateParentOnboardingProfileAsync(id, req));

        Assert.Equal("So luong tre phai lon hon hoac bang 1.", ex.Message);
    }

    // Condition: tổng số con khai báo &lt; số hồ sơ con đã tạo.
    [Fact]
    public async Task NumberOfChildrenLessThanCreatedProfiles_Throws()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _mockParent.Setup(p => p.FindByUserIdAsync(id))
            .ReturnsAsync(new ParentProfile { Id = parentId, UserId = id });
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(parentId))
            .ReturnsAsync(new List<ChildProfile> { new(), new(), new() });
        _mockParent.Setup(p => p.SaveChangesAsync()).Returns(Task.CompletedTask);

        var req = new UpdateParentProfileRequest { NumberOfChildren = 2, FamilyDescription = "F" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateParentOnboardingProfileAsync(id, req));

        Assert.Equal("Khong the giam tong so tre xuong 2 vi ban da tao 3 ho so tre.", ex.Message);
    }

    // Condition: chưa có ParentProfile — tạo mới.
    [Fact]
    public async Task NoParentProfile_CreatesAndSaves()
    {
        var id = Guid.NewGuid();
        _mockParent.Setup(p => p.FindByUserIdAsync(id)).ReturnsAsync((ParentProfile?)null);
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<ChildProfile>());
        _mockParent.Setup(p => p.SaveChangesAsync()).Returns(Task.CompletedTask);

        var req = new UpdateParentProfileRequest
        {
            FamilyDescription = "Gia đình A",
            NumberOfChildren = 2
        };
        await _sut.UpdateParentOnboardingProfileAsync(id, req);

        _mockParent.Verify(p => p.Add(It.Is<ParentProfile>(x => x.UserId == id)), Times.Once);
        _mockParent.Verify(p => p.SaveChangesAsync(), Times.Once);
    }

    // Condition: đã có profile — cập nhật mô tả và số con.
    [Fact]
    public async Task ExistingProfile_Updates()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var p = new ParentProfile { Id = parentId, UserId = id, NumberOfChildren = 1, FamilyDescription = "old" };
        _mockParent.Setup(x => x.FindByUserIdAsync(id)).ReturnsAsync(p);
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(parentId))
            .ReturnsAsync(new List<ChildProfile>());
        _mockParent.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);

        await _sut.UpdateParentOnboardingProfileAsync(id, new UpdateParentProfileRequest
        {
            FamilyDescription = "Mới",
            NumberOfChildren = 3
        });

        Assert.Equal("Mới", p.FamilyDescription);
        Assert.Equal(3, p.NumberOfChildren);
        Assert.Equal(id, p.UpdatedBy);
        Assert.NotNull(p.UpdatedAt);
    }

    // Condition: bỏ trống NumberOfChildren, profile chưa set — dùng số con đang có.
    [Fact]
    public async Task FillsNumberOfChildrenFromCreatedWhenUnset()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var p = new ParentProfile
        {
            Id = parentId,
            UserId = id,
            NumberOfChildren = null,
            FamilyDescription = "F"
        };
        _mockParent.Setup(x => x.FindByUserIdAsync(id)).ReturnsAsync(p);
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(parentId))
            .ReturnsAsync(new List<ChildProfile> { new(), new() });
        _mockParent.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);

        await _sut.UpdateParentOnboardingProfileAsync(id, new UpdateParentProfileRequest
        {
            FamilyDescription = "F2",
            NumberOfChildren = null
        });

        Assert.Equal(2, p.NumberOfChildren);
        Assert.Equal("F2", p.FamilyDescription);
    }
}
