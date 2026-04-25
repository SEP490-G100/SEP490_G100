using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// VerificationRequestController.ModeratorViewVerificationList ->
/// VerificationRequestService.ModeratorViewVerificationListAsync
/// </summary>
public class ModeratorViewVerificationListAsyncTests
{
    private readonly Mock<IVerificationRequestRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly VerificationRequestService _sut;

    public ModeratorViewVerificationListAsyncTests()
    {
        _mockRepo = new Mock<IVerificationRequestRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new VerificationRequestService(_mockRepo.Object, _mockNotif.Object);
    }

    private static VerificationRequest MakeListItem(Guid id, string first = "Mei", string last = "Tran")
    {
        var userId = Guid.NewGuid();
        var nannyProfileId = Guid.NewGuid();
        return new VerificationRequest
        {
            Id = id,
            NannyProfileId = nannyProfileId,
            RequestType = 1,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
            NannyProfile = new NannyProfile
            {
                Id = nannyProfileId,
                UserId = userId,
                User = new User
                {
                    Id = userId,
                    FirstName = first,
                    LastName = last,
                    Email = "n@n.n",
                    City = "SG"
                }
            },
            VerificationDocuments = new List<VerificationDocument>()
        };
    }

    // Condition: page < 1 -> normalized to 1 before repository call.
    [Fact]
    public async Task PageBelowOne_NormalizesTo1()
    {
        _mockRepo
            .Setup(r => r.GetModeratorListAsync(1, 2, "x", 1, 10))
            .ReturnsAsync((new List<VerificationRequest>(), 0));

        var r = await _sut.ModeratorViewVerificationListAsync(1, 2, "x", 0, 10);

        Assert.Equal(1, r.Page);
        _mockRepo.Verify(r => r.GetModeratorListAsync(1, 2, "x", 1, 10), Times.Once);
    }

    // Condition: pageSize out of range -> normalized to 3.
    [Fact]
    public async Task PageSizeOutOfRange_NormalizesTo3()
    {
        _mockRepo
            .Setup(r => r.GetModeratorListAsync(null, null, null, 1, 3))
            .ReturnsAsync((new List<VerificationRequest>(), 0));

        var r = await _sut.ModeratorViewVerificationListAsync(null, null, null, 1, 0);

        Assert.Equal(3, r.PageSize);
        _mockRepo.Verify(r => r.GetModeratorListAsync(null, null, null, 1, 3), Times.Once);
    }

    // Condition: pageSize > 100 -> normalized to 3.
    [Fact]
    public async Task PageSizeOver100_NormalizesTo3()
    {
        _mockRepo
            .Setup(r => r.GetModeratorListAsync(null, null, null, 1, 3))
            .ReturnsAsync((new List<VerificationRequest>(), 0));

        var r = await _sut.ModeratorViewVerificationListAsync(null, null, null, 1, 200);

        Assert.Equal(3, r.PageSize);
        _mockRepo.Verify(r => r.GetModeratorListAsync(null, null, null, 1, 3), Times.Once);
    }

    // Condition: valid response -> map items and preserve totalCount.
    [Fact]
    public async Task ReturnsMappedListAndTotal()
    {
        var requestId = Guid.NewGuid();
        var item = MakeListItem(requestId, "A", "B");

        _mockRepo
            .Setup(r => r.GetModeratorListAsync(0, 1, "q", 2, 20))
            .ReturnsAsync((new List<VerificationRequest> { item }, 7));

        var r = await _sut.ModeratorViewVerificationListAsync(0, 1, "q", 2, 20);

        Assert.Equal(7, r.TotalCount);
        Assert.Equal(2, r.Page);
        Assert.Equal(20, r.PageSize);
        Assert.Single(r.Items);
        Assert.Equal(requestId, r.Items[0].Id);
        Assert.Equal("A", r.Items[0].NannyFirstName);
        Assert.Equal("B", r.Items[0].NannyLastName);
        Assert.Equal("n@n.n", r.Items[0].NannyEmail);

        _mockRepo.Verify(r => r.GetModeratorListAsync(0, 1, "q", 2, 20), Times.Once);
    }
}
