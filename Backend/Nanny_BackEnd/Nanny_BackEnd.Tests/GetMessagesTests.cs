using Microsoft.AspNetCore.SignalR;
using Moq;
using Nanny_BackEnd.Controllers;
using Nanny_BackEnd.Hubs;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="CommunicationController.GetMessages"/> → <see cref="CommunicationService.GetMessageHistoryAsync"/>.
/// </summary>
public class GetMessagesTests
{
    private readonly Mock<ICommunicationRepository> _mockRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<IUserRepository>         _mockUser;
    private readonly Mock<IReportService>         _mockReport;
    private readonly Mock<IHubContext<ChatHub>>  _mockHub;
    private readonly CommunicationService         _sut;

    public GetMessagesTests()
    {
        _mockRepo  = new Mock<ICommunicationRepository>();
        _mockNotif = new Mock<INotificationService>();
        _mockUser  = new Mock<IUserRepository>();
        _mockReport = new Mock<IReportService>();
        _mockHub   = new Mock<IHubContext<ChatHub>>();
        _sut = new CommunicationService(
            _mockRepo.Object,
            _mockNotif.Object,
            _mockUser.Object,
            _mockReport.Object,
            _mockHub.Object);
    }

    private static User U(Guid id, string? first, string? last, string? email = "u@t.c", string? avatar = null) =>
        new() { Id = id, FirstName = first ?? "", LastName = last ?? "", Email = email ?? "", AvatarUrl = avatar };

    private static Conversation BuildConv(
        Guid convId,
        Guid me,
        Guid other,
        bool myBlocked = false,
        bool myHidden = false,
        Guid? myUpdatedBy = null,
        string? otherFirst = "O", string? otherLast = "1") => new()
    {
        Id = convId,
        ConversationParticipants = new List<ConversationParticipant>
        {
            new()
            {
                UserId    = me,
                IsBlocked = myBlocked,
                IsHidden  = myHidden,
                UpdatedBy = myUpdatedBy,
                User      = U(me, "M", "E")
            },
            new() { UserId = other, User = U(other, otherFirst, otherLast, "o@e.com", "av.png") }
        }
    };

    // Condition: conversation không tồn tại.
    // Confirmation: KeyNotFoundException, không tải messages.
    [Fact]
    public async Task NotFound_Throws_DoesNotLoadMessages()
    {
        var cid = Guid.NewGuid();
        var me  = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetConversationByIdAsync(cid)).ReturnsAsync((Conversation?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GetMessageHistoryAsync(cid, me, 1, 30));

        _mockRepo.Verify(r => r.GetMessagesByConversationIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // Condition: current user không nằm trong participants.
    [Fact]
    public async Task NotMember_Throws()
    {
        var cid = Guid.NewGuid();
        var me  = Guid.NewGuid();
        var a   = Guid.NewGuid();
        var b   = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetConversationByIdAsync(cid)).ReturnsAsync(BuildConv(cid, a, b));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _sut.GetMessageHistoryAsync(cid, me, 1, 30));

        _mockRepo.Verify(r => r.GetMessagesByConversationIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // Condition: trang 1, 1 tin, map đúng.
    // Confirmation: OtherUser, TotalCount, Message IsMine, gọi repo với skip=0.
    [Fact]
    public async Task Success_MapsHeaderAndMessages_CallsRepoWithSkipZero()
    {
        var convId = Guid.NewGuid();
        var me     = Guid.NewGuid();
        var oth    = Guid.NewGuid();
        var msgId  = Guid.NewGuid();
        var t      = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var conv   = BuildConv(convId, me, oth);
        var message = new Message
        {
            Id             = msgId,
            ConversationId = convId,
            SenderUserId   = oth,
            Content        = "hi",
            MessageType    = 1,
            IsDeleted      = false,
            CreatedAt      = t,
            ReadAt         = null,
            SenderUser     = U(oth, "O", "1")
        };

        _mockRepo.Setup(r => r.GetConversationByIdAsync(convId)).ReturnsAsync(conv);
        _mockRepo.Setup(r => r.GetMessagesByConversationIdAsync(convId, 0, 30))
            .ReturnsAsync(([message], 1));

        var dto = await _sut.GetMessageHistoryAsync(convId, me, page: 1, pageSize: 30);

        Assert.Equal(convId, dto.ConversationId);
        Assert.Equal(oth, dto.OtherUserId);
        Assert.Equal("O 1", dto.OtherUserName);
        Assert.Equal(1, dto.TotalCount);
        Assert.Equal(1, dto.Page);
        Assert.Equal(30, dto.PageSize);
        Assert.False(dto.IsBlocked);
        Assert.False(dto.IsHidden);
        Assert.Null(dto.BlockedByUserId);

        var m = Assert.Single(dto.Messages);
        Assert.Equal(msgId, m.Id);
        Assert.Equal("hi", m.Content);
        Assert.False(m.IsMine);
        Assert.Equal(t, m.CreatedAt);

        _mockRepo.Verify(r => r.GetMessagesByConversationIdAsync(convId, 0, 30), Times.Once);
    }

    // Condition: page=2, pageSize=10.
    // Confirmation: skip=10, take=10.
    [Fact]
    public async Task Pagination_UsesSkipFromPage()
    {
        var convId = Guid.NewGuid();
        var me     = Guid.NewGuid();
        var oth    = Guid.NewGuid();
        var conv   = BuildConv(convId, me, oth);

        _mockRepo.Setup(r => r.GetConversationByIdAsync(convId)).ReturnsAsync(conv);
        _mockRepo.Setup(r => r.GetMessagesByConversationIdAsync(convId, 10, 10))
            .ReturnsAsync((new List<Message>(), 25));

        var dto = await _sut.GetMessageHistoryAsync(convId, me, page: 2, pageSize: 10);

        Assert.Equal(2, dto.Page);
        Assert.Equal(10, dto.PageSize);
        Assert.Equal(25, dto.TotalCount);
        _mockRepo.Verify(r => r.GetMessagesByConversationIdAsync(convId, 10, 10), Times.Once);
    }

    // Condition: page<1 và pageSize ngoài [1,100] biên dịch về 1..100.
    // pageSize clamp [1,100] (0 → 1, không dùng 12 như danh sách hội thoại).
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(1, 200, 0, 100)]
    public async Task Boundary_ClampPageAndPageSize(int pageIn, int sizeIn, int expectSkip, int expectTake)
    {
        var convId = Guid.NewGuid();
        var me     = Guid.NewGuid();
        var oth    = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetConversationByIdAsync(convId)).ReturnsAsync(BuildConv(convId, me, oth));
        _mockRepo.Setup(r => r.GetMessagesByConversationIdAsync(convId, expectSkip, expectTake))
            .ReturnsAsync((new List<Message>(), 0));

        _ = await _sut.GetMessageHistoryAsync(convId, me, pageIn, sizeIn);

        _mockRepo.Verify(r => r.GetMessagesByConversationIdAsync(convId, expectSkip, expectTake), Times.Once);
    }

    // Condition: cuộc hội thoại bị chặn phía tôi.
    [Fact]
    public async Task MyParticipant_Blocked_ExposesBlockedBy()
    {
        var blockBy = Guid.NewGuid();
        var convId  = Guid.NewGuid();
        var me      = Guid.NewGuid();
        var oth     = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetConversationByIdAsync(convId))
            .ReturnsAsync(BuildConv(convId, me, oth, myBlocked: true, myUpdatedBy: blockBy));
        _mockRepo.Setup(r => r.GetMessagesByConversationIdAsync(convId, 0, 30))
            .ReturnsAsync((new List<Message>(), 0));

        var dto = await _sut.GetMessageHistoryAsync(convId, me, 1, 30);

        Assert.True(dto.IsBlocked);
        Assert.Equal(blockBy, dto.BlockedByUserId);
    }

    // Condition: tin nhắn đã xóa.
    [Fact]
    public async Task DeletedMessage_PlaceholderContent()
    {
        var convId = Guid.NewGuid();
        var me     = Guid.NewGuid();
        var oth    = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetConversationByIdAsync(convId)).ReturnsAsync(BuildConv(convId, me, oth));
        var message = new Message
        {
            Id             = Guid.NewGuid(),
            ConversationId = convId,
            SenderUserId   = me,
            Content        = "old",
            IsDeleted      = true,
            MessageType    = 1,
            CreatedAt      = DateTime.UtcNow,
            SenderUser     = U(me, "M", "E")
        };
        _mockRepo.Setup(r => r.GetMessagesByConversationIdAsync(convId, 0, 30))
            .ReturnsAsync(([message], 1));

        var dto = await _sut.GetMessageHistoryAsync(convId, me, 1, 30);

        var m = Assert.Single(dto.Messages);
        Assert.Equal("[Tin nhan da bi xoa]", m.Content);
        Assert.Null(m.AttachmentUrl);
    }
}
