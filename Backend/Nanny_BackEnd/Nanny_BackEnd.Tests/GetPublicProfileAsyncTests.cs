using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ProfileController"/> public profile ?
/// <see cref="ProfileService.GetPublicProfileAsync"/>.
/// </summary>
public class GetPublicProfileAsyncTests
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

    public GetPublicProfileAsyncTests()
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

    private static User MakeUserWithContact(Guid id) => new()
    {
        Id = id,
        Email = "parent@x.com",
        FirstName = "F",
        LastName = "L",
        PhoneNumber = "0900000000",
        DateOfBirth = new DateOnly(1988, 3, 15),
        City = "HCM",
        District = "Q1",
        Ward = "P1",
        Latitude = 10.5m,
        Longitude = 106.5m
    };

    private void SetupParentWithChild(Guid userId, Guid parentProfileId, Guid childId)
    {
        _mockUser.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(MakeUserWithContact(userId));
        _mockUser.Setup(u => u.GetRolesAsync(userId)).ReturnsAsync(new List<string> { "parent" });
        _mockParent.Setup(p => p.FindByUserIdAsync(userId)).ReturnsAsync(new ParentProfile
        {
            Id = parentProfileId,
            UserId = userId,
            NumberOfChildren = 1
        });
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(parentProfileId))
            .ReturnsAsync(new List<ChildProfile>
            {
                new()
                {
                    Id = childId,
                    ParentProfileId = parentProfileId,
                    SpecialNeeds = "S",
                    Notes = "N",
                    Characteristic = "Ch",
                    ChildAgeGroup = 1,
                    CreatedAt = DateTime.UtcNow
                }
            });
    }

    [Fact]
    public async Task TargetUserNotFound_ThrowsInvalidOperation()
    {
        var requester = Guid.NewGuid();
        var target = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(target)).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.GetPublicProfileAsync(requester, target));

    }

    [Fact]
    public async Task SelfView_ReturnsFullProfileUnchanged()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        SetupParentWithChild(id, parentId, childId);

        var dto = await _sut.GetPublicProfileAsync(id, id);

        Assert.Equal("parent@x.com", dto.Email);
        Assert.Equal("0900000000", dto.PhoneNumber);
        Assert.NotNull(dto.DateOfBirth);
        Assert.NotNull(dto.Children);
        Assert.Single(dto.Children!);
    }

    [Fact]
    public async Task OtherParentViewer_StripsPrivateAndFamilyFields()
    {
        var target = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        SetupParentWithChild(target, parentId, childId);

        var dto = await _sut.GetPublicProfileAsync(requester, target);

        Assert.Equal(string.Empty, dto.Email);
        Assert.Null(dto.PhoneNumber);
        Assert.Null(dto.DateOfBirth);
        Assert.Null(dto.Age);
        Assert.Null(dto.Address);
        Assert.Null(dto.Ward);
        Assert.Null(dto.Latitude);
        Assert.Null(dto.Longitude);
        Assert.Null(dto.FamilyDescription);
        Assert.Null(dto.NumberOfChildren);
        Assert.Null(dto.Children);
        Assert.Null(dto.SpecialNeeds);
        Assert.Null(dto.Notes);
        Assert.Null(dto.Characteristic);
        Assert.Null(dto.ChildAgeGroup);

        Assert.Equal("F", dto.FirstName);
        Assert.Equal("L", dto.LastName);
        // City / District are not cleared by this branch in ProfileService.
        Assert.Equal("HCM", dto.City);
        Assert.Equal("Q1", dto.District);
    }

    [Fact]
    public async Task OtherNannyViewer_KeepsContactAndLocation()
    {
        var target = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var nannyProfileId = Guid.NewGuid();
        var user = MakeUserWithContact(target);
        _mockUser.Setup(u => u.FindByIdAsync(target)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(target)).ReturnsAsync(new List<string> { "nanny" });
        _mockNannyProfile.Setup(n => n.FindByUserIdAsync(target)).ReturnsAsync(new NannyProfile
        {
            Id = nannyProfileId,
            UserId = target,
            VerificationStatus = (int)VerificationStatus.Approved
        });
        _mockNannySkill.Setup(s => s.GetByNannyProfileIdAsync(nannyProfileId))
            .ReturnsAsync(new List<NannySkill>());
        _mockNannyAvail.Setup(a => a.GetByNannyProfileIdAsync(nannyProfileId))
            .ReturnsAsync(new List<NannyAvailability>());
        _mockNannyCert.Setup(c => c.GetByNannyProfileIdAsync(nannyProfileId))
            .ReturnsAsync(new List<NannyCertificate>());
        _mockVerification.Setup(v => v.GetRequestsByNannyProfileAsync(nannyProfileId))
            .ReturnsAsync(new List<VerificationRequest>());

        var dto = await _sut.GetPublicProfileAsync(requester, target);

        Assert.Equal("parent@x.com", dto.Email);
        Assert.Equal("0900000000", dto.PhoneNumber);
        Assert.NotNull(dto.DateOfBirth);
        Assert.Equal("P1", dto.Ward);
        Assert.Equal(10.5m, dto.Latitude);
        Assert.Equal(106.5m, dto.Longitude);
    }

    [Fact]
    public async Task OtherViewer_NeitherParentNorNanny_StripsCorePII()
    {
        var target = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var user = MakeUserWithContact(target);
        _mockUser.Setup(u => u.FindByIdAsync(target)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(target)).ReturnsAsync(new List<string> { "moderator" });

        var dto = await _sut.GetPublicProfileAsync(requester, target);

        Assert.Equal(string.Empty, dto.Email);
        Assert.Null(dto.PhoneNumber);
        Assert.Null(dto.DateOfBirth);
        Assert.Null(dto.Age);
        Assert.Null(dto.Address);
        Assert.Null(dto.Ward);
        Assert.Null(dto.Latitude);
        Assert.Null(dto.Longitude);
        Assert.Equal("F", dto.FirstName);
    }
}
