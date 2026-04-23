using Moq;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ModeratorVerificationController.ModeratorViewVerificationList"/> →
/// <see cref="ModeratorVerificationService.ModeratorViewVerificationListAsync"/>.
/// </summary>
public class ModeratorViewVerificationListAsyncTests
{
    private readonly Mock<IModeratorVerificationRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly ModeratorVerificationService _sut;

    public ModeratorViewVerificationListAsyncTests()
    {
        _mockRepo = new Mock<IModeratorVerificationRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new ModeratorVerificationService(_mockRepo.Object, _mockNotif.Object);
    }

    private static VerificationRequest MakeListItem(Guid id, string first = "Mei", string last = "Tran")
    {
        var userId = Guid.NewGuid();
        var npId = Guid.NewGuid();
        return new VerificationRequest
        {
            Id = id,
            NannyProfileId = npId,
            RequestType = 0,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
            NannyProfile = new NannyProfile
            {
                Id = npId,
                UserId = userId,
                User = new User
                {
                    Id = userId,
                    Email = "n@n.n",
                    FirstName = first,
                    LastName = last,
                    City = "SG"
                }
            },
            VerificationDocuments = new List<VerificationDocument>()
        };
    }

    // Condition: page dưới 1 được nâng lên 1 trước khi gọi repo.
    [Fact]
    public async Task PageBelowOne_NormalizesTo1()
    {
        _mockRepo
            .Setup(r => r.GetListAsync(1, 2, "x", 1, 10))
            .ReturnsAsync((new List<VerificationRequest>(), 0));

        var r = await _sut.ModeratorViewVerificationListAsync(1, 2, "x", 0, 10);

        Assert.Equal(1, r.Page);
        _mockRepo.Verify(
            r => r.GetListAsync(1, 2, "x", 1, 10),
            Times.Once);
    }

    // Condition: pageSize ngoài khoảng 1–100 → 3.
    [Fact]
    public async Task PageSizeOutOfRange_NormalizesTo3()
    {
        _mockRepo
            .Setup(r => r.GetListAsync(null, null, null, 1, 3))
            .ReturnsAsync((new List<VerificationRequest>(), 0));

        var r = await _sut.ModeratorViewVerificationListAsync(null, null, null, 1, 0);

        Assert.Equal(3, r.PageSize);
        _mockRepo.Verify(r => r.GetListAsync(null, null, null, 1, 3), Times.Once);
    }

    // Condition: pageSize trên 100 → 3.
    [Fact]
    public async Task PageSizeOver100_NormalizesTo3()
    {
        _mockRepo
            .Setup(r => r.GetListAsync(null, null, null, 1, 3))
            .ReturnsAsync((new List<VerificationRequest>(), 0));

        var r = await _sut.ModeratorViewVerificationListAsync(null, null, null, 1, 200);

        Assert.Equal(3, r.PageSize);
        _mockRepo.Verify(r => r.GetListAsync(null, null, null, 1, 3), Times.Once);
    }

    // Condition: hợp lệ — giữ nguyên page/pageSize, map Items và TotalCount.
    [Fact]
    public async Task ReturnsMappedListAndTotal()
    {
        var reqId = Guid.NewGuid();
        var item = MakeListItem(reqId, "A", "B");
        _mockRepo
            .Setup(r => r.GetListAsync(0, 1, "q", 2, 20))
            .ReturnsAsync((new List<VerificationRequest> { item }, 7));

        var r = await _sut.ModeratorViewVerificationListAsync(0, 1, "q", 2, 20);

        Assert.Equal(7, r.TotalCount);
        Assert.Equal(2, r.Page);
        Assert.Equal(20, r.PageSize);
        Assert.Single(r.Items);
        Assert.Equal(reqId, r.Items[0].Id);
        Assert.Equal("A", r.Items[0].NannyFirstName);
        Assert.Equal("B", r.Items[0].NannyLastName);
        Assert.Equal("n@n.n", r.Items[0].NannyEmail);
        _mockRepo.Verify(r => r.GetListAsync(0, 1, "q", 2, 20), Times.Once);
    }
}
