
using Moq;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="Nanny_BackEnd.Controllers.NannyVerificationRequestController.NannyGetVerificationRequestList"/> →
/// <see cref="VerificationRequestService.NannyGetVerificationRequestListAsync"/>.
/// </summary>
public class NannyGetVerificationRequestListAsyncTests
{
    private readonly Mock<IVerificationRequestRepository> _mockRepo;
    private readonly Mock<INotificationService> _mockNotif;
    private readonly VerificationRequestService _sut;

    public NannyGetVerificationRequestListAsyncTests()
    {
        _mockRepo = new Mock<IVerificationRequestRepository>();
        _mockNotif = new Mock<INotificationService>();
        _sut = new VerificationRequestService(_mockRepo.Object, _mockNotif.Object);
    }

    private static NannyProfile MakeNannyProfile(Guid userId, Guid npId) => new()
    {
        Id = npId,
        UserId = userId,
        CreatedAt = DateTime.UtcNow,
        User = new User
        {
            Id = userId,
            Email = "n@n.n",
            FirstName = "A",
            LastName = "B",
            City = "SG"
        },
        NannySkills = new List<NannySkill>(),
        NannyCertificates = new List<NannyCertificate>()
    };

    private static VerificationRequest MakeVr(
        Guid id,
        NannyProfile np,
        int status,
        int requestType = 1)
    {
        var exp = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);
        return new VerificationRequest
        {
            Id = id,
            NannyProfileId = np.Id,
            NannyProfile = np,
            RequestType = requestType,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            VerificationDocuments = new List<VerificationDocument>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DocumentType = (int)VerificationDocumentType.IdentityCard,
                    DocumentUrl = "https://x/d.pdf",
                    FileName = "d.pdf",
                    IsDeleted = false,
                    ExpiryDate = exp
                }
            }
        };
    }

    // Condition: chưa có hồ sơ nanny — không lấy request, vẫn trả về phân trang.
    [Fact]
    public async Task NoNannyProfile_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync((NannyProfile?)null);

        var r = await _sut.NannyGetVerificationRequestListAsync(userId, null, 0, 10);

        Assert.Empty(r.Items);
        Assert.Equal(0, r.TotalCount);
        Assert.Equal(1, r.Page);
        Assert.Equal(10, r.PageSize);
        _mockRepo.Verify(x => x.GetRequestsByNannyProfileAsync(It.IsAny<Guid>()), Times.Never);
    }

    // Condition: page / pageSize ngoài phạm vi được chuẩn hóa.
    [Fact]
    public async Task NormalizesPageAndPageSize()
    {
        var userId = Guid.NewGuid();
        var npId = Guid.NewGuid();
        var np = MakeNannyProfile(userId, npId);
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetRequestsByNannyProfileAsync(npId))
            .ReturnsAsync(new List<VerificationRequest>());

        var r = await _sut.NannyGetVerificationRequestListAsync(userId, null, 0, 0);

        Assert.Equal(1, r.Page);
        Assert.Equal(3, r.PageSize);
    }

    // Condition: lọc theo status.
    [Fact]
    public async Task FiltersByStatus()
    {
        var userId = Guid.NewGuid();
        var npId = Guid.NewGuid();
        var np = MakeNannyProfile(userId, npId);
        var p = (int)NannyVerificationRequestStatus.Pending;
        var a = (int)NannyVerificationRequestStatus.Approved;
        var l = new List<VerificationRequest>
        {
            MakeVr(Guid.NewGuid(), np, p),
            MakeVr(Guid.NewGuid(), np, p),
            MakeVr(Guid.NewGuid(), np, a)
        };
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetRequestsByNannyProfileAsync(npId)).ReturnsAsync(l);

        var r = await _sut.NannyGetVerificationRequestListAsync(userId, p, 1, 10);

        Assert.Equal(2, r.TotalCount);
        Assert.Equal(2, r.Items.Count);
        Assert.All(r.Items, i => Assert.Equal(p, i.Status));
    }

    // Condition: phân trang in-memory.
    [Fact]
    public async Task Paging_SkipTake()
    {
        var userId = Guid.NewGuid();
        var npId = Guid.NewGuid();
        var np = MakeNannyProfile(userId, npId);
        var ids = new[]
        {
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            Guid.Parse("10000000-0000-0000-0000-000000000005")
        };
        var l = ids.Select(id => MakeVr(id, np, (int)NannyVerificationRequestStatus.Pending))
            .ToList();
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetRequestsByNannyProfileAsync(npId)).ReturnsAsync(l);

        var r = await _sut.NannyGetVerificationRequestListAsync(userId, null, 2, 2);

        Assert.Equal(5, r.TotalCount);
        Assert.Equal(2, r.Items.Count);
        Assert.Equal(ids[2], r.Items[0].Id);
        Assert.Equal(ids[3], r.Items[1].Id);
    }
}
