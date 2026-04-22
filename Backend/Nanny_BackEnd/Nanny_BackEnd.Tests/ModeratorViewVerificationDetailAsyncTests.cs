using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ModeratorVerificationController.ModeratorViewVerificationDetail"/> →
/// <see cref="ModeratorVerificationService.ModeratorViewVerificationDetailAsync"/>.
/// </summary>
public class ModeratorViewVerificationDetailAsyncTests
{
    private const string NotFoundMessage = "Không tìm thấy yêu cầu xác minh.";

    private readonly Mock<IModeratorVerificationRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly ModeratorVerificationService _sut;

    public ModeratorViewVerificationDetailAsyncTests()
    {
        _mockRepo = new Mock<IModeratorVerificationRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new ModeratorVerificationService(_mockRepo.Object, _mockNotif.Object);
    }

    private static VerificationRequest MakeRequest(Guid id, Guid? reviewerId = null, string? reviewerFirst = "Mod")
    {
        var userId = Guid.NewGuid();
        var npId = Guid.NewGuid();
        var vr = new VerificationRequest
        {
            Id = id,
            NannyProfileId = npId,
            RequestType = 1,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            ReviewedBy = reviewerId,
            ReviewedAt = reviewerId != null ? DateTime.UtcNow : null,
            NannyProfile = new NannyProfile
            {
                Id = npId,
                UserId = userId,
                SalaryType = 0,
                VerificationStatus = 0,
                CreatedAt = DateTime.UtcNow,
                NannySkills = new List<NannySkill>(),
                NannyCertificates = new List<NannyCertificate>(),
                User = new User
                {
                    Id = userId,
                    Email = "a@a.a",
                    FirstName = "Hoa",
                    LastName = "Mai"
                }
            },
            VerificationDocuments = new List<VerificationDocument>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DocumentType = 0,
                    DocumentUrl = "https://x/u.pdf",
                    FileName = "u.pdf",
                    FileSize = 100,
                    IsDeleted = false
                }
            }
        };
        if (reviewerId.HasValue)
        {
            vr.ReviewedByNavigation = new User
            {
                Id = reviewerId.Value,
                Email = "m@mod.com",
                FirstName = reviewerFirst!,
                LastName = "Erator"
            };
        }
        return vr;
    }

    // Condition: không tìm thấy bản ghi.
    [Fact]
    public async Task NotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((VerificationRequest?)null);

        var (success, data, message) = await _sut.ModeratorViewVerificationDetailAsync(id);

        Assert.False(success);
        Assert.Null(data);
        Assert.Equal(NotFoundMessage, message);
    }

    // Condition: có dữ liệu — map sang detail DTO.
    [Fact]
    public async Task Found_ReturnsMappedDetail()
    {
        var id = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var req = MakeRequest(id, modId, "Linh");
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(req);

        var (success, data, message) = await _sut.ModeratorViewVerificationDetailAsync(id);

        Assert.True(success);
        Assert.Null(message);
        Assert.NotNull(data);
        Assert.Equal(id, data!.Id);
        Assert.Equal(1, data.RequestType);
        Assert.Equal("Hoa", data.NannyFirstName);
        Assert.Equal("Mai", data.NannyLastName);
        Assert.Equal("a@a.a", data.NannyEmail);
        Assert.Equal("Linh Erator", data.ReviewedByName);
        Assert.Single(data.Documents);
        Assert.Equal(0, data.Documents[0].DocumentType);
        Assert.Empty(data.Skills);
        Assert.Empty(data.Certificates);
    }
}
