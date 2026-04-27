using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ProfileController.DeleteChildProfile"/> ?
/// <see cref="ProfileService.DeleteChildProfileAsync"/>.
/// </summary>
public class DeleteChildProfileAsyncTests
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

    public DeleteChildProfileAsyncTests()
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

    private const string ParentNotFoundMessage = "Không tìm thấy hồ sơ Parent.";
    private const string ChildNotFoundMessage = "Không tìm thấy con.";

    [Fact]
    public async Task NotParent_Throws()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.DeleteChildProfileAsync(id, childId));

    }

    [Fact]
    public async Task ParentNotFound_Throws()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(id)).ReturnsAsync((ParentProfile?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.DeleteChildProfileAsync(id, childId));

        Assert.Equal(ParentNotFoundMessage, ex.Message);
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
            _sut.DeleteChildProfileAsync(id, childId));

        Assert.Equal(ChildNotFoundMessage, ex.Message);
    }

    [Fact]
    public async Task Success_SoftDeletesAndSaves()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _mockUser.Setup(u => u.GetRolesAsync(userId)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(userId))
            .ReturnsAsync(new ParentProfile { Id = parentId, UserId = userId });
        var child = new ChildProfile
        {
            Id = childId,
            ParentProfileId = parentId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        _mockChild.Setup(c => c.FindByIdAndParentAsync(childId, parentId)).ReturnsAsync(child);
        _mockChild.Setup(c => c.SaveChangesAsync()).Returns(Task.CompletedTask);

        await _sut.DeleteChildProfileAsync(userId, childId);

        _mockChild.Verify(
            c => c.Update(It.Is<ChildProfile>(ch =>
                ch.Id == childId
                && ch.IsDeleted
                && ch.UpdatedBy == userId
                && ch.UpdatedAt != null)), Times.Once);
        _mockChild.Verify(c => c.SaveChangesAsync(), Times.Once);
    }
}
