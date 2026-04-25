using Moq;
using Nanny_BackEnd.DTOs.Review;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.ReviewController.Create"/> ?
/// <see cref="ReviewService.CreateReviewAsync"/>.
/// </summary>
public class CreateReviewAsyncTests
{
    private readonly Mock<IReviewRepository> _mockReview;
    private readonly Mock<IHiringRepository> _mockHiring;
    private readonly Mock<IUserRepository> _mockUser;
    private readonly ReviewService _sut;

    public CreateReviewAsyncTests()
    {
        _mockReview = new Mock<IReviewRepository>();
        _mockHiring = new Mock<IHiringRepository>();
        _mockUser = new Mock<IUserRepository>();
        _sut = new ReviewService(_mockReview.Object, _mockHiring.Object, _mockUser.Object);
    }

    private static HiringRecord MakeHiring(
        Guid hiringId,
        Guid parentUserId,
        Guid nannyUserId,
        int status)
    {
        var ppId = Guid.NewGuid();
        var npId = Guid.NewGuid();
        return new HiringRecord
        {
            Id = hiringId,
            Status = status,
            ParentProfileId = ppId,
            NannyProfileId = npId,
            StartDate = new DateOnly(2025, 1, 1),
            CreatedAt = DateTime.UtcNow,
            ParentProfile = new ParentProfile
            {
                Id = ppId,
                UserId = parentUserId,
                User = new User { Id = parentUserId, FirstName = "P", LastName = "A" }
            },
            NannyProfile = new NannyProfile
            {
                Id = npId,
                UserId = nannyUserId,
                User = new User { Id = nannyUserId, FirstName = "N", LastName = "B" }
            }
        };
    }

    [Fact]
    public async Task HiringNotFound_ThrowsKeyNotFound()
    {
        var pid = Guid.NewGuid();
        var hid = Guid.NewGuid();
        _mockHiring.Setup(h => h.GetHiringRecordByIdAsync(hid)).ReturnsAsync((HiringRecord?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateReviewAsync(pid,
            new CreateReviewRequest { HiringRecordId = hid, Rating = 5 }));

    }

    [Fact]
    public async Task WrongParent_ThrowsUnauthorized()
    {
        var parentId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var nanny = Guid.NewGuid();
        var hid = Guid.NewGuid();
        _mockHiring.Setup(h => h.GetHiringRecordByIdAsync(hid))
            .ReturnsAsync(MakeHiring(hid, parentId, nanny, (int)HiringRecordStatus.Completed));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.CreateReviewAsync(other,
            new CreateReviewRequest { HiringRecordId = hid, Rating = 4 }));

    }

    [Fact]
    public async Task NotCompleted_ThrowsInvalidOperation()
    {
        var parentId = Guid.NewGuid();
        var nanny = Guid.NewGuid();
        var hid = Guid.NewGuid();
        _mockHiring.Setup(h => h.GetHiringRecordByIdAsync(hid))
            .ReturnsAsync(MakeHiring(hid, parentId, nanny, (int)HiringRecordStatus.Active));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateReviewAsync(parentId,
            new CreateReviewRequest { HiringRecordId = hid, Rating = 4 }));

    }

    [Fact]
    public async Task AlreadyReviewed_Throws()
    {
        var parentId = Guid.NewGuid();
        var nanny = Guid.NewGuid();
        var hid = Guid.NewGuid();
        _mockHiring.Setup(h => h.GetHiringRecordByIdAsync(hid))
            .ReturnsAsync(MakeHiring(hid, parentId, nanny, (int)HiringRecordStatus.Completed));
        _mockReview.Setup(r => r.ExistsAsync(hid, parentId)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateReviewAsync(parentId,
            new CreateReviewRequest { HiringRecordId = hid, Rating = 4 }));

    }

    [Fact]
    public async Task ReviewerUserMissing_ThrowsAfterSave()
    {
        var parentId = Guid.NewGuid();
        var nanny = Guid.NewGuid();
        var hid = Guid.NewGuid();
        _mockHiring.Setup(h => h.GetHiringRecordByIdAsync(hid))
            .ReturnsAsync(MakeHiring(hid, parentId, nanny, (int)HiringRecordStatus.Completed));
        _mockReview.Setup(r => r.ExistsAsync(hid, parentId)).ReturnsAsync(false);
        _mockUser.Setup(u => u.FindByIdAsync(parentId)).ReturnsAsync((User?)null);
        _mockReview.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateReviewAsync(parentId,
            new CreateReviewRequest { HiringRecordId = hid, Rating = 5, Comment = "  ok  " }));

        _mockReview.Verify(r => r.Add(It.IsAny<Review>()), Times.Once);
        _mockReview.Verify(r => r.SaveChangesAsync(), Times.Once);
    }


    [Fact]
    public async Task Success_AddsSavesAndReturnsDto()
    {
        var parentId = Guid.NewGuid();
        var nanny = Guid.NewGuid();
        var hid = Guid.NewGuid();
        _mockHiring.Setup(h => h.GetHiringRecordByIdAsync(hid))
            .ReturnsAsync(MakeHiring(hid, parentId, nanny, (int)HiringRecordStatus.Completed));
        _mockReview.Setup(r => r.ExistsAsync(hid, parentId)).ReturnsAsync(false);
        var parentUser = new User
        {
            Id = parentId,
            FirstName = "Mỹ",
            LastName = "Lan",
            AvatarUrl = "/a.png"
        };
        _mockUser.Setup(u => u.FindByIdAsync(parentId)).ReturnsAsync(parentUser);
        _mockReview.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var dto = await _sut.CreateReviewAsync(parentId, new CreateReviewRequest
        {
            HiringRecordId = hid,
            Rating = 5,
            Comment = "  Tốt  "
        });

        Assert.Equal(5, dto.Rating);
        Assert.Equal("Tốt", dto.Comment);
        Assert.Equal("Mỹ Lan", dto.ReviewerName);
        Assert.Equal("/a.png", dto.ReviewerAvatarUrl);
        _mockReview.Verify(
            r => r.Add(It.Is<Review>(x =>
                x.HiringRecordId == hid
                && x.ReviewerUserId == parentId
                && x.RevieweeUserId == nanny
                && x.Rating == 5
                && x.Comment == "Tốt"
                && !x.IsDeleted)),
            Times.Once);
    }
}
