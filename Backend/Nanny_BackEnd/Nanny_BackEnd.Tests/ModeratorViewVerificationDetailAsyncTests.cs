using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// VerificationRequestController.ModeratorViewVerificationDetail ->
/// VerificationRequestService.ModeratorViewVerificationDetailAsync
/// </summary>
public class ModeratorViewVerificationDetailAsyncTests
{
    private const string NotFoundMessage = "Không tìm thấy yêu cầu xác minh.";

    private readonly Mock<IVerificationRequestRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly VerificationRequestService _sut;

    public ModeratorViewVerificationDetailAsyncTests()
    {
        _mockRepo = new Mock<IVerificationRequestRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new VerificationRequestService(_mockRepo.Object, _mockNotif.Object);
    }

    private static VerificationRequest MakeRequest(Guid id, Guid? reviewerId = null, string reviewerFirstName = "Mod")
    {
        var userId = Guid.NewGuid();
        var nannyProfileId = Guid.NewGuid();
        var request = new VerificationRequest
        {
            Id = id,
            NannyProfileId = nannyProfileId,
            RequestType = 1,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            ReviewedBy = reviewerId,
            ReviewedAt = reviewerId.HasValue ? DateTime.UtcNow : null,
            NannyProfile = new NannyProfile
            {
                Id = nannyProfileId,
                UserId = userId,
                SalaryType = 0,
                VerificationStatus = 1,
                CreatedAt = DateTime.UtcNow,
                NannySkills = new List<NannySkill>(),
                NannyCertificates = new List<NannyCertificate>(),
                User = new User
                {
                    Id = userId,
                    FirstName = "Hoa",
                    LastName = "Mai",
                    Email = "a@a.a"
                }
            },
            VerificationDocuments = new List<VerificationDocument>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DocumentType = 1,
                    DocumentUrl = "https://x/u.pdf",
                    FileName = "u.pdf",
                    FileSize = 100,
                    IsDeleted = false
                }
            }
        };

        if (reviewerId.HasValue)
        {
            request.ReviewedByNavigation = new User
            {
                Id = reviewerId.Value,
                FirstName = reviewerFirstName,
                LastName = "Erator",
                Email = "m@mod.com"
            };
        }

        return request;
    }

    // Condition: request not found.
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

    // Condition: found -> map to detail dto.
    [Fact]
    public async Task Found_ReturnsMappedDetail()
    {
        var id = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var request = MakeRequest(id, reviewerId, "Linh");
        _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(request);

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
        Assert.Equal(1, data.Documents[0].DocumentType);
        Assert.Empty(data.Skills);
        Assert.Empty(data.Certificates);
    }
}
