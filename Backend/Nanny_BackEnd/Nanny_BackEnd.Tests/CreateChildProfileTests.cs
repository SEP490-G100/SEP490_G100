using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nanny_BackEnd.DTOs.Profile;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ProfileController.CreateChildProfile"/> →
/// <see cref="ProfileService.CreateChildProfileAsync"/>.
/// </summary>
public class CreateChildProfileTests
{
    private const string OnlyParentMessage = "Chá»‰ dÃ nh cho Parent.";

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

    public CreateChildProfileTests()
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

    private void StubSaves()
    {
        _mockParent.Setup(p => p.SaveChangesAsync()).Returns(Task.CompletedTask);
        _mockChild.Setup(c => c.SaveChangesAsync()).Returns(Task.CompletedTask);
    }

    private void StubOneChildForAnyParent()
    {
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid parentId) => new List<ChildProfile>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ParentProfileId = parentId,
                    CreatedAt = DateTime.UtcNow
                }
            });
    }

    private static CreateChildProfileRequest ValidRequest() => new()
    {
        ChildAgeGroup = ChildAgeGroup.Toddler,
        SpecialNeeds = "  ok  ",
        Notes = null,
        Characteristic = "X"
    };

    // Condition: không phải Parent.
    [Fact]
    public async Task NotParent_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.CreateChildProfileAsync(id, ValidRequest()));

        Assert.Equal(OnlyParentMessage, ex.Message);
        _mockParent.Verify(p => p.FindByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: ChildAgeGroup null / không hợp lệ.
    [Fact]
    public async Task InvalidChildAgeGroup_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        var req = new CreateChildProfileRequest { ChildAgeGroup = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.CreateChildProfileAsync(id, req));

        Assert.Equal("Nhóm tuổi của trẻ không hợp lệ.", ex.Message);
    }

    // Condition: chuỗi tùy chọn vượt quá maxLength.
    [Fact]
    public async Task SpecialNeedsTooLong_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        var req = new CreateChildProfileRequest
        {
            ChildAgeGroup = ChildAgeGroup.Baby,
            SpecialNeeds = new string('x', 1001)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.CreateChildProfileAsync(id, req));

        Assert.Equal("Nhu cầu đặc biệt không được vượt quá 1000 ký tự.", ex.Message);
    }

    // Condition: đã có ParentProfile — tạo Child, Ensure tăng NumberOfChildren nếu cần.
    [Fact]
    public async Task Success_ExistingParent_AddsChildAndMayUpdateDeclaredCount()
    {
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(userId)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(userId)).ReturnsAsync(new ParentProfile
        {
            Id = parentId,
            UserId = userId,
            NumberOfChildren = null
        });
        StubSaves();
        StubOneChildForAnyParent();

        var req = ValidRequest();
        var dto = await _sut.CreateChildProfileAsync(userId, req);

        _mockChild.Verify(
            c => c.Add(It.Is<ChildProfile>(ch =>
                ch.ParentProfileId == parentId
                && ch.SpecialNeeds == "ok"
                && ch.Notes == null
                && ch.Characteristic == "X"
                && ch.ChildAgeGroup == (byte)ChildAgeGroup.Toddler)),
            Times.Once);
        _mockChild.Verify(c => c.SaveChangesAsync(), Times.Once);
        _mockParent.Verify(p => p.SaveChangesAsync(), Times.Once);

        Assert.Equal(parentId, dto.ParentProfileId);
        Assert.Equal((byte)ChildAgeGroup.Toddler, dto.ChildAgeGroup);
    }

    // Condition: chưa có ParentProfile — tạo Parent rồi tạo Child.
    [Fact]
    public async Task Success_NoParentProfile_CreatesParentThenChild()
    {
        var userId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(userId)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(userId)).ReturnsAsync((ParentProfile?)null);
        StubSaves();
        StubOneChildForAnyParent();

        var dto = await _sut.CreateChildProfileAsync(userId, ValidRequest());

        _mockParent.Verify(p => p.Add(It.IsAny<ParentProfile>()), Times.Once);
        _mockParent.Verify(p => p.SaveChangesAsync(), Times.AtLeastOnce);
        _mockChild.Verify(c => c.Add(It.IsAny<ChildProfile>()), Times.Once);
        _mockChild.Verify(c => c.SaveChangesAsync(), Times.Once);
        Assert.NotEqual(Guid.Empty, dto.Id);
    }
}
