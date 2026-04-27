using Microsoft.AspNetCore.SignalR;
using Moq;
using Nanny_BackEnd.Hubs;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="CommunicationController.GetConversations"/> → <see cref="CommunicationService.GetConversationsAsync"/>.
/// </summary>
public class GetConversationsAsyncTests
{
    private readonly Mock<ICommunicationRepository> _mockRepo;
    private readonly Mock<INotificationService>    _mockNotif;
    private readonly Mock<IUserRepository>       _mockUser;
    private readonly Mock<IReportService>        _mockReport;
    private readonly Mock<IHubContext<ChatHub>>  _mockHub;
    private readonly CommunicationService         _sut;

    public GetConversationsAsyncTests()
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

    private static Conversation Conv(
        Guid id,
        List<ConversationParticipant> parts,
        List<Message> messages,
        DateTime? lastAt = null) =>
        new()
        {
            Id = id,
            LastMessageAt = lastAt,
            ConversationParticipants = parts,
            Messages = messages
        };

    [Fact]
    public async Task Empty_ReturnsNoItems()
    {
        var me = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetConversationsByUserIdAsync(me)).ReturnsAsync(new List<Conversation>());

        var list = await _sut.GetConversationsAsync(me);

        Assert.Empty(list);
    }

    // Condition: 1-1, tin mới nhất từ đối phương, chưa đọc.
    // Confirmation: OtherUser*, LastMessage, UnreadCount=1.
    [Fact]
    public async Task Normal_MapsOtherUser_LastMessage_Unread()
    {
        var me  = Guid.NewGuid();
        var oth = Guid.NewGuid();
        var cid = Guid.NewGuid();
        var t1  = new DateTime(2025, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var t2  = new DateTime(2025, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(), ConversationId = cid, SenderUserId = me, Content = "old",
                CreatedAt = t1, IsDeleted = false, ReadAt = DateTime.UtcNow, SenderUser = U(me, "A", "B")
            },
            new()
            {
                Id = Guid.NewGuid(), ConversationId = cid, SenderUserId = oth, Content = "newest",
                CreatedAt = t2, IsDeleted = false, ReadAt = null, SenderUser = U(oth, "X", "Y")
            }
        };
        var conv = Conv(
            cid,
            new List<ConversationParticipant>
            {
                new() { UserId = me, IsBlocked = false, IsHidden = false, User = U(me, "A", "B") },
                new() { UserId = oth, IsBlocked = false, IsHidden = false, User = U(oth, "X", "Y", avatar: "z.png") }
            },
            messages,
            lastAt: t2);

        _mockRepo.Setup(r => r.GetConversationsByUserIdAsync(me)).ReturnsAsync(new List<Conversation> { conv });

        var list = await _sut.GetConversationsAsync(me);
        var item = Assert.Single(list);

        Assert.Equal(cid, item.Id);
        Assert.Equal(oth, item.OtherUserId);
        Assert.Equal("X Y", item.OtherUserName);
        Assert.Equal("z.png", item.OtherUserAvatar);
        Assert.Equal("newest", item.LastMessage);
        Assert.Equal(t2, item.LastMessageAt);
        Assert.False(item.IsBlocked);
        Assert.False(item.IsHidden);
        Assert.Equal(1, item.UnreadCount);
    }

    // Confirmation: LastMessage placeholder.
    [Fact]
    public async Task LastMessageDeleted_ShowsPlaceholder()
    {
        var me  = Guid.NewGuid();
        var oth = Guid.NewGuid();
        var cid = Guid.NewGuid();
        var t   = DateTime.UtcNow;
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid(), ConversationId = cid, SenderUserId = oth, Content = "gone",
                CreatedAt = t, IsDeleted = true, SenderUser = U(oth, "P", "Q")
            }
        };
        var conv = Conv(
            cid,
            new List<ConversationParticipant>
            {
                new() { UserId = me, User = U(me, "A", "B") },
                new() { UserId = oth, User = U(oth, "P", "Q") }
            },
            messages);

        _mockRepo.Setup(r => r.GetConversationsByUserIdAsync(me)).ReturnsAsync(new List<Conversation> { conv });

        var item = Assert.Single((await _sut.GetConversationsAsync(me)));

        Assert.Equal("[Tin nhắn đã bị xóa]", item.LastMessage);
    }

    [Fact]
    public async Task OtherUser_NoName_UsesEmail()
    {
        var me  = Guid.NewGuid();
        var oth = Guid.NewGuid();
        var cid = Guid.NewGuid();
        var conv = Conv(
            cid,
            new List<ConversationParticipant>
            {
                new() { UserId = me, User = U(me, "A", "B") },
                new() { UserId = oth, User = U(oth, " ", "  ", "only@email.com") }
            },
            new List<Message>());

        _mockRepo.Setup(r => r.GetConversationsByUserIdAsync(me)).ReturnsAsync(new List<Conversation> { conv });

        var item = Assert.Single((await _sut.GetConversationsAsync(me)));

        Assert.Equal("only@email.com", item.OtherUserName);
    }

    [Fact]
    public async Task MyParticipant_Blocked_ExposedInDto()
    {
        var me  = Guid.NewGuid();
        var oth = Guid.NewGuid();
        var cid = Guid.NewGuid();
        var conv = Conv(
            cid,
            new List<ConversationParticipant>
            {
                new() { UserId = me, IsBlocked = true, IsHidden = true, User = U(me, "A", "B") },
                new() { UserId = oth, User = U(oth, "C", "D") }
            },
            new List<Message>());

        _mockRepo.Setup(r => r.GetConversationsByUserIdAsync(me)).ReturnsAsync(new List<Conversation> { conv });

        var item = Assert.Single((await _sut.GetConversationsAsync(me)));

        Assert.True(item.IsBlocked);
        Assert.True(item.IsHidden);
    }
}
