using Microsoft.AspNetCore.SignalR;
using Moq;
using Nanny_BackEnd.Controllers;
using Nanny_BackEnd.DTOs.Communication;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Hubs;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Tests;

/// <summary>
/// <see cref="CommunicationController.SendMessage"/> → <see cref="CommunicationService.SendMessageAsync"/>.
/// </summary>
public class SendMessageTests
{
    private readonly Mock<ICommunicationRepository> _mockRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<IUserRepository>         _mockUser;
    private readonly Mock<IReportService>          _mockReport;
    private readonly Mock<IHubContext<ChatHub>>   _mockHub;
    private readonly CommunicationService         _sut;

    public SendMessageTests()
    {
        _mockRepo   = new Mock<ICommunicationRepository>();
        _mockNotif  = new Mock<INotificationService>();
        _mockUser   = new Mock<IUserRepository>();
        _mockReport = new Mock<IReportService>();
        _mockHub    = new Mock<IHubContext<ChatHub>>();
        _sut = new CommunicationService(
            _mockRepo.Object,
            _mockNotif.Object,
            _mockUser.Object,
            _mockReport.Object,
            _mockHub.Object);
    }

    private static User U(Guid id, string f, string l) =>
        new() { Id = id, FirstName = f, LastName = l, Email = "x@x.com" };

    private static SendMessageDto Dto(Guid convId, string content) =>
        new()
        {
            ConversationId = convId,
            Content        = content,
            MessageType    = 1
        };

    // Condition: nội dung rỗng / khoảng trắng.
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task EmptyContent_Throws(string? content)
    {
        var dto = Dto(Guid.NewGuid(), content ?? "");
        if (content == null) dto.Content = null!;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _sut.SendMessageAsync(dto, Guid.NewGuid()));
        Assert.Equal("Noi dung tin nhan khong duoc de trong.", ex.Message);
    }

    // Condition: sender không phải participant.
    [Fact]
    public async Task NotParticipant_Throws()
    {
        _mockRepo.Setup(r => r.GetParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((ConversationParticipant?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _sut.SendMessageAsync(Dto(Guid.NewGuid(), "hi"), Guid.NewGuid()));

        Assert.Equal("Ban khong phai thanh vien cua cuoc hoi thoai nay.", ex.Message);
        _mockRepo.Verify(r => r.AddMessage(It.IsAny<Message>()), Times.Never);
    }

    // Condition: hội thoại bị chặn.
    [Fact]
    public async Task Blocked_Throws()
    {
        var cid = Guid.NewGuid();
        var me  = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetParticipantAsync(cid, me)).ReturnsAsync(new ConversationParticipant
        {
            UserId    = me,
            IsBlocked = true,
            User      = U(me, "A", "B")
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SendMessageAsync(Dto(cid, "x"), me));

        Assert.Equal("Cuoc hoi thoai nay dang bi chan. Ban khong the gui tin nhan.", ex.Message);
    }

    // Condition: luồng bình thường, không có moderator ở phía còn lại.
    [Fact]
    public async Task Success_AddsMessage_Save_NoModeratorNotification()
    {
        var cid  = Guid.NewGuid();
        var me   = Guid.NewGuid();
        var oth  = Guid.NewGuid();
        var conv = new Conversation { Id = cid, LastMessageAt = null };
        var myP  = new ConversationParticipant { UserId = me, IsBlocked = false, User = U(me, "S", "Ender") };

        _mockRepo.Setup(r => r.GetParticipantAsync(cid, me)).ReturnsAsync(myP);
        _mockRepo.Setup(r => r.GetConversationByIdAsync(cid)).ReturnsAsync(conv);
        _mockRepo.Setup(r => r.GetMessageByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Message?)null);
        _mockRepo.Setup(r => r.GetParticipantsByConversationIdAsync(cid))
            .ReturnsAsync(new List<ConversationParticipant>
            {
                myP,
                new() { UserId = oth }
            });
        _mockUser.Setup(u => u.HasRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>())).ReturnsAsync(false);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await _sut.SendMessageAsync(Dto(cid, "  hello  "), me);

        Assert.Equal("hello", result.Content);
        Assert.True(result.IsMine);
        Assert.Equal("S Ender", result.SenderName);
        _mockRepo.Verify(r => r.AddMessage(It.Is<Message>(m => m.Content == "hello" && m.SenderUserId == me && m.ConversationId == cid)), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockNotif.Verify(
            n => n.createNotificationForUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    // Condition: thành viên còn lại là Moderator.
    // Confirmation: thông báo tới moderator.
    [Fact]
    public async Task Success_NotifiesWhenOtherParticipantIsModerator()
    {
        var cid = Guid.NewGuid();
        var me  = Guid.NewGuid();
        var mod = Guid.NewGuid();
        var myP = new ConversationParticipant { UserId = me, IsBlocked = false, User = U(me, "A", "B") };

        _mockRepo.Setup(r => r.GetParticipantAsync(cid, me)).ReturnsAsync(myP);
        _mockRepo.Setup(r => r.GetConversationByIdAsync(cid)).ReturnsAsync(new Conversation { Id = cid });
        _mockRepo.Setup(r => r.GetMessageByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Message?)null);
        _mockRepo.Setup(r => r.GetParticipantsByConversationIdAsync(cid))
            .ReturnsAsync(new List<ConversationParticipant> { myP, new() { UserId = mod } });
        _mockUser.Setup(u => u.HasRolesAsync(It.IsAny<Guid>(), It.IsAny<string[]>()))
            .ReturnsAsync((Guid uid, string[] _) => uid == mod);
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        _ = await _sut.SendMessageAsync(Dto(cid, "mod ping"), me);

        _mockNotif.Verify(
            n => n.createNotificationForUsers(
                It.Is<IEnumerable<Guid>>(g => g.Single() == mod),
                "Co tin nhan moi gui toi moderator",
                "A B vua gui mot tin nhan moi can moderator phan hoi.",
                NotificationTypes.MessageToModerator,
                cid,
                "Conversation",
                me),
            Times.Once);
    }
}
