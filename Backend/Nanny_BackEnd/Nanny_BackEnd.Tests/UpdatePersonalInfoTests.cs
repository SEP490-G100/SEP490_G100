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
/// <see cref="Nanny_BackEnd.Controllers.ProfileController.UpdatePersonalInfo"/> →
/// <see cref="ProfileService.UpdatePersonalInfoAsync"/>.
/// </summary>
public class UpdatePersonalInfoTests
{
    private const string UserNotFoundMessage = "Người dùng không tồn tại.";

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

    public UpdatePersonalInfoTests()
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

    private void StubGeoNoCoords()
    {
        _mockGeo.Setup(g => g.geocode(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(default((decimal, decimal)?));
    }

    private void StubPersonalProfileForUser(Guid userId, User user, IReadOnlyList<string> roles)
    {
        _mockUser.Setup(u => u.FindByIdAsync(userId)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(userId)).ReturnsAsync(roles.ToList());
    }

    // Condition: user không tồn tại.
    [Fact]
    public async Task UserNotFound_Throws()
    {
        var id = Guid.NewGuid();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync((User?)null);
        var req = new UpdatePersonalInfoRequest { FirstName = "A", LastName = "B" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdatePersonalInfoAsync(id, req));

        Assert.Equal(UserNotFoundMessage, ex.Message);
        _mockUser.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    // Condition: DOB lớn hơn hôm nay.
    [Fact]
    public async Task FutureDateOfBirth_Throws()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = "a@a.a",
            FirstName = "O",
            LastName = "G",
        };
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "parent" });
        var farFuture = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var req = new UpdatePersonalInfoRequest { FirstName = "A", LastName = "B", DateOfBirth = farFuture };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdatePersonalInfoAsync(id, req));

        Assert.Equal("Ngày sinh không được lớn hơn ngày hiện tại.", ex.Message);
    }

        // Condition: bảo mẫu — lương từ ngoài khoảng cho phép.
    [Fact]
    public async Task Nanny_SalaryOutOfRange_Throws()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = "n@n.n",
            FirstName = "N",
            LastName = "A",
            DateOfBirth = new DateOnly(1980, 1, 1)
        };
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });
        var req = new UpdatePersonalInfoRequest
        {
            FirstName = "N",
            LastName = "A",
            DateOfBirth = new DateOnly(1980, 1, 1),
            ExpectedSalaryMin = 1_000_000m,
            ExpectedSalaryMax = 9_000_000m
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdatePersonalInfoAsync(id, req));

        Assert.Equal("Lương từ phải trong khoảng 8.000.000 - 50.000.000 VND.", ex.Message);
    }

    // Condition: bảo mẫu — không có DOB ở request và cũng không có trên user.
    [Fact]
    public async Task Nanny_MissingDob_Throws()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = "n@n.n",
            FirstName = "N",
            LastName = "A",
            DateOfBirth = null
        };
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });
        var req = new UpdatePersonalInfoRequest
        {
            FirstName = "N",
            LastName = "A",
            DateOfBirth = null,
            ExpectedSalaryMin = 10_000_000m,
            ExpectedSalaryMax = 20_000_000m
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdatePersonalInfoAsync(id, req));

        Assert.Equal("Nanny phải nhập ngày sinh.", ex.Message);
    }

    // Condition: bảo mẫu — tuổi ≤ 30 (tính theo ngày hiện tại).
    [Fact]
    public async Task Nanny_Age30OrUnder_Throws()
    {
        var id = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.Today);
        // 10 năm trước → chắc chắn dưới 31 tuổi so với rule "phải lớn hơn 30".
        var youngDob = today.AddYears(-10);
        var user = new User
        {
            Id = id,
            Email = "n@n.n",
            FirstName = "N",
            LastName = "A",
            DateOfBirth = youngDob
        };
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });
        var req = new UpdatePersonalInfoRequest
        {
            FirstName = "N",
            LastName = "A",
            DateOfBirth = youngDob,
            ExpectedSalaryMin = 10_000_000m,
            ExpectedSalaryMax = 20_000_000m
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdatePersonalInfoAsync(id, req));

        Assert.Equal("Nanny phải lớn hơn 30 tuổi.", ex.Message);
    }

    // Condition: không phải bảo mẫu — cập nhật tên, gọi lưu.
    // Confirmation: DTO trả về tên mới, SaveChanges đúng 1 lần.
    [Fact]
    public async Task Success_Parent_UpdatesNameAndSaves()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = "p@p.p",
            FirstName = "Old",
            LastName = "Name",
        };
        StubPersonalProfileForUser(id, user, new[] { "parent" });
        StubGeoNoCoords();
        _mockUser.Setup(u => u.SaveChangesAsync()).Returns(Task.CompletedTask);

        var req = new UpdatePersonalInfoRequest { FirstName = "Mới", LastName = "Họ" };
        var dto = await _sut.UpdatePersonalInfoAsync(id, req);

        Assert.Equal("Mới", dto.FirstName);
        Assert.Equal("Họ", dto.LastName);
        _mockUser.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // Condition: bảo mẫu hợp lệ: trên 30 tuổi, lương trong khoảng, có sẵn NannyProfile.
    [Fact]
    public async Task Success_Nanny_UpdatesUserAndNanny_Saves()
    {
        var id = Guid.NewGuid();
        var nannyId = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = "n@n.n",
            FirstName = "A",
            LastName = "B",
            DateOfBirth = new DateOnly(1980, 6, 1)
        };
        StubGeoNoCoords();
        _mockUser.Setup(u => u.FindByIdAsync(id)).ReturnsAsync(user);
        _mockUser.Setup(u => u.GetRolesAsync(id)).ReturnsAsync(new List<string> { "nanny" });
        _mockUser.Setup(u => u.SaveChangesAsync()).Returns(Task.CompletedTask);

        _mockNannyProfile.Setup(n => n.FindByUserIdAsync(id)).ReturnsAsync(new NannyProfile
        {
            Id = nannyId,
            UserId = id,
            VerificationStatus = (int)VerificationStatus.Approved
        });
        _mockNannySkill.Setup(s => s.GetByNannyProfileIdAsync(nannyId))
            .ReturnsAsync(new List<NannySkill>());
        _mockNannyAvail.Setup(a => a.GetByNannyProfileIdAsync(nannyId))
            .ReturnsAsync(new List<NannyAvailability>());
        _mockNannyCert.Setup(c => c.GetByNannyProfileIdAsync(nannyId))
            .ReturnsAsync(new List<NannyCertificate>());
        _mockVerification.Setup(v => v.GetRequestsByNannyProfileAsync(nannyId))
            .ReturnsAsync(new List<VerificationRequest>());

        var req = new UpdatePersonalInfoRequest
        {
            FirstName = "A2",
            LastName = "B2",
            DateOfBirth = new DateOnly(1980, 6, 1),
            Bio = "  Bio line  ",
            YearsOfExperience = 5,
            EducationLevel = 2,
            ExpectedSalaryMin = 10_000_000m,
            ExpectedSalaryMax = 20_000_000m,
            MaxTravelDistance = 30
        };

        var dto = await _sut.UpdatePersonalInfoAsync(id, req);

        Assert.Equal("A2", dto.FirstName);
        Assert.Equal("B2", dto.LastName);
        Assert.Equal("Bio line", dto.Bio);
        Assert.Equal(5, dto.YearsOfExperience);
        Assert.Equal(2, dto.EducationLevel);
        _mockUser.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    // Condition: geocode trả tọa độ — ưu tiên ghi user.Latitude/Longitude từ geocode.
    [Fact]
    public async Task GeocodeResult_PrefersCoordsOverRequestLatLong()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Email = "u@u.u",
            FirstName = "A",
            LastName = "B",
            City = "C",
            District = "D"
        };
        StubPersonalProfileForUser(id, user, new[] { "other" });
        _mockGeo.Setup(g => g.geocode(It.IsAny<string?>(), "HCM", "Q1"))
            .ReturnsAsync((11m, 22m));
        _mockUser.Setup(u => u.SaveChangesAsync()).Returns(Task.CompletedTask);

        var req = new UpdatePersonalInfoRequest
        {
            FirstName = "A",
            LastName = "B",
            City = "HCM",
            District = "Q1",
            Latitude = 1m,
            Longitude = 2m
        };
        var dto = await _sut.UpdatePersonalInfoAsync(id, req);

        Assert.Equal(11m, dto.Latitude);
        Assert.Equal(22m, dto.Longitude);
    }
}
