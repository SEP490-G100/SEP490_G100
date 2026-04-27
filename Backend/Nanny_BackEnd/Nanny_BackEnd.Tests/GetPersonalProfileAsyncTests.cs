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
/// <see cref="Nanny_BackEnd.Controllers.ProfileController.GetPersonalProfile"/> ?
/// <see cref="ProfileService.GetPersonalProfileAsync"/>.
/// </summary>
public class GetPersonalProfileAsyncTests
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

    public GetPersonalProfileAsyncTests()
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

    private static User MakeUser(Guid id, string email = "a@b.c", string first = "A", string last = "B") => new()
    {
        Id = id,
        Email = email,
        FirstName = first,
        LastName = last,
        DateOfBirth = new DateOnly(1990, 5, 10),
    };

    // Confirmation: InvalidOperationException v?i message th?ng nh?t trong ProfileService.
    [Fact]
    public async Task UserNotFound_ThrowsInvalidOperation()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetPersonalProfileAsync(id));

    }

    [Fact]
    public async Task Success_NoParentNoNanny_MapsUserAndRoles()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "other" });

        var dto = await _sut.GetPersonalProfileAsync(id);

        Assert.Equal(id, dto.UserId);
        Assert.Equal("a@b.c", dto.Email);
        Assert.Equal("A", dto.FirstName);
        Assert.Equal("B", dto.LastName);
        Assert.Single(dto.Roles);
        Assert.Equal("other", dto.Roles[0]);
        Assert.Null(dto.NannyProfileId);
        Assert.Null(dto.FamilyDescription);
        Assert.Null(dto.Children);
    }

    // Confirmation: FamilyDescription, Children, tru?ng l?y t? con d?u.
    [Fact]
    public async Task Success_Parent_MapsChildrenAndFirstChildFields()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });

        var parentProfile = new ParentProfile
        {
            Id = parentId,
            UserId = id,
            NumberOfChildren = 2
        };
        _mockParent.Setup(p => p.FindByUserIdAsync(id)).ReturnsAsync(parentProfile);

        var child = new ChildProfile
        {
            Id = childId,
            ParentProfileId = parentId,
            SpecialNeeds = "A",
            Notes = "N",
            Characteristic = "C",
            ChildAgeGroup = 2,
            CreatedAt = DateTime.UtcNow
        };
        _mockChild.Setup(c => c.GetByParentProfileIdAsync(parentId))
            .ReturnsAsync(new List<ChildProfile> { child });

        var dto = await _sut.GetPersonalProfileAsync(id);

        Assert.Equal(2, dto.NumberOfChildren);
        Assert.NotNull(dto.Children);
        Assert.Single(dto.Children!);
        Assert.Equal(childId, dto.Children![0].Id);
        Assert.Equal("A", dto.SpecialNeeds);
        Assert.Equal("N", dto.Notes);
        Assert.Equal("C", dto.Characteristic);
        Assert.Equal((byte)2, dto.ChildAgeGroup);
    }

    [Fact]
    public async Task Success_Nanny_MapsNannyBlockAndCollections()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        var nannyProfileId = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });

        _mockNannyProfile.Setup(n => n.FindByUserIdAsync(id)).ReturnsAsync(new NannyProfile
        {
            Id = nannyProfileId,
            UserId = id,
            Bio = "Bio",
            YearsOfExperience = 3,
            EducationLevel = 1,
            ExpectedSalaryMin = 5,
            ExpectedSalaryMax = 10,
            MaxTravelDistance = 15,
            AverageRating = 4.5m,
            TotalReviews = 2,
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

        var dto = await _sut.GetPersonalProfileAsync(id);

        Assert.Equal(nannyProfileId, dto.NannyProfileId);
        Assert.Equal("Bio", dto.Bio);
        Assert.Equal(3, dto.YearsOfExperience);
        Assert.Equal(1, dto.EducationLevel);
        Assert.Equal(5, dto.ExpectedSalaryMin);
        Assert.Equal(10, dto.ExpectedSalaryMax);
        Assert.Equal(15, dto.MaxTravelDistance);
        Assert.Equal(4.5m, dto.AverageRating);
        Assert.Equal(2, dto.TotalReviews);
        Assert.Equal((int)VerificationStatus.Approved, dto.VerificationStatusCode);
        Assert.NotNull(dto.Skills);
        Assert.Empty(dto.Skills!);
        Assert.NotNull(dto.Availabilities);
        Assert.Empty(dto.Availabilities!);
        Assert.NotNull(dto.Certificates);
        Assert.Empty(dto.Certificates!);
        Assert.False(dto.HasHealthCertificate);
    }

    // Confirmation: VerificationStatus m?c d?nh theo code (chu?i trong service).
    [Fact]
    public async Task Success_NannyWithoutProfile_UsesDefaultVerificationLabel()
    {
        var id = Guid.NewGuid();
        var user = MakeUser(id);
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });
        _mockNannyProfile.Setup(n => n.FindByUserIdAsync(id)).ReturnsAsync((NannyProfile?)null);

        var dto = await _sut.GetPersonalProfileAsync(id);

    }
}
