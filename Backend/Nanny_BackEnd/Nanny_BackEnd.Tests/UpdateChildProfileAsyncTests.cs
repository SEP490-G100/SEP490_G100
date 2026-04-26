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
/// <see cref="Nanny_BackEnd.Controllers.ProfileController.UpdateChildProfile"/> ?
/// <see cref="ProfileService.UpdateChildProfileAsync"/>.
/// </summary>
public class UpdateChildProfileAsyncTests
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
    private readonly Mock<ISubscriptionService> _mockSub;
    private readonly ProfileService _sut;

    public UpdateChildProfileAsyncTests()
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
        _mockSub = new Mock<ISubscriptionService>();
        _mockSub.Setup(s => s.tryGrantWelcomeTrialAsync(It.IsAny<Guid>(), It.IsAny<string>())).Returns(Task.CompletedTask);

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
            _mockSub.Object,
            NullLogger<ProfileService>.Instance);
    }

    private const string ParentNotFoundMessage = "Không tìm thấy hồ sơ Parent.";
    private const string ChildNotFoundMessage = "Không tìm thấy con hoặc không có quyền.";

    private static UpdateChildProfileRequest ValidRequest() => new()
    {
        ChildAgeGroup = ChildAgeGroup.Preschooler,
        SpecialNeeds = "S",
        Notes = "N",
        Characteristic = "C"
    };

    [Fact]
    public async Task NotParent_Throws()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.UpdateChildProfileAsync(id, childId, ValidRequest()));

    }

    [Fact]
    public async Task InvalidChildAgeGroup_Throws()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        var req = new UpdateChildProfileRequest { ChildAgeGroup = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateChildProfileAsync(id, childId, req));

    }

    [Fact]
    public async Task NotesTooLong_Throws()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        var req = new UpdateChildProfileRequest
        {
            ChildAgeGroup = ChildAgeGroup.Baby,
            Notes = new string('n', 1001)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateChildProfileAsync(id, childId, req));

    }

    [Fact]
    public async Task ParentNotFound_Throws()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(id)).ReturnsAsync((ParentProfile?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateChildProfileAsync(id, childId, ValidRequest()));

        Assert.Equal(ParentNotFoundMessage, ex.Message);
        _mockChild.Verify(
            c => c.FindByIdAndParentAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ChildNotFound_Throws()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(id))
            .ReturnsAsync(new ParentProfile { Id = parentId, UserId = id });
        _mockChild.Setup(c => c.FindByIdAndParentAsync(childId, parentId))
            .ReturnsAsync((ChildProfile?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateChildProfileAsync(id, childId, ValidRequest()));

        Assert.Equal(ChildNotFoundMessage, ex.Message);
    }

    [Fact]
    public async Task Success_UpdatesAndReturnsDto()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(userId)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(userId))
            .ReturnsAsync(new ParentProfile { Id = parentId, UserId = userId });
        var existing = new ChildProfile
        {
            Id = childId,
            ParentProfileId = parentId,
            SpecialNeeds = "oldS",
            Notes = "oldN",
            Characteristic = "oldC",
            ChildAgeGroup = 0,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _mockChild.Setup(c => c.FindByIdAndParentAsync(childId, parentId)).ReturnsAsync(existing);
        _mockChild.Setup(c => c.SaveChangesAsync()).Returns(Task.CompletedTask);

        var req = new UpdateChildProfileRequest
        {
            ChildAgeGroup = ChildAgeGroup.Gradeschooler,
            SpecialNeeds = "  s2  ",
            Notes = "n2",
            Characteristic = "c2"
        };
        var dto = await _sut.UpdateChildProfileAsync(userId, childId, req);

        _mockChild.Verify(c => c.Update(It.Is<ChildProfile>(ch =>
            ch.Id == childId
            && ch.SpecialNeeds == "s2"
            && ch.Notes == "n2"
            && ch.Characteristic == "c2"
            && ch.ChildAgeGroup == (byte)ChildAgeGroup.Gradeschooler
            && ch.UpdatedBy == userId
            && ch.UpdatedAt != null)), Times.Once);
        _mockChild.Verify(c => c.SaveChangesAsync(), Times.Once);
        Assert.Equal(childId, dto.Id);
        Assert.Equal(parentId, dto.ParentProfileId);
        Assert.Equal("s2", dto.SpecialNeeds);
        Assert.Equal("n2", dto.Notes);
        Assert.Equal("c2", dto.Characteristic);
        Assert.Equal((byte)ChildAgeGroup.Gradeschooler, dto.ChildAgeGroup);
    }
}
