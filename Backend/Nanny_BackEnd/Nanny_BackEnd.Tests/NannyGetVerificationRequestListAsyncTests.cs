
using Moq;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
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
    }

    [Fact]
    public async Task ReturnsRequests()
    {
        var userId = Guid.NewGuid();
        var npId   = Guid.NewGuid();
        var np     = MakeNannyProfile(userId, npId);
        var id1    = Guid.NewGuid();
        var id2    = Guid.NewGuid();
        var list   = new List<VerificationRequest>
        {
            MakeVr(id1, np, (int)NannyVerificationRequestStatus.Pending),
            MakeVr(id2, np, (int)NannyVerificationRequestStatus.Approved)
        };
        _mockRepo.Setup(r => r.GetNannyProfileByUserIdAsync(userId)).ReturnsAsync(np);
        _mockRepo.Setup(r => r.GetRequestsByNannyProfileAsync(npId)).ReturnsAsync(list);

        var r = await _sut.NannyGetVerificationRequestListAsync(userId, null, 1, 10);

        Assert.Equal(2, r.TotalCount);
        Assert.Equal(2, r.Items.Count);
        Assert.Contains(r.Items, i => i.Id == id1);
        Assert.Contains(r.Items, i => i.Id == id2);
    }

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
}
