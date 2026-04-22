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
/// <see cref="CommunicationController.GetOrCreateConversation"/> → <see cref="CommunicationService.GetOrCreateConversationAsync"/>.
/// </summary>
public class GetOrCreateConversationTests
{
    private readonly Mock<ICommunicationRepository> _mockRepo;
    private readonly Mock<INotificationService>     _mockNotif;
    private readonly Mock<IUserRepository>            _mockUser;
    private readonly Mock<IReportService>            _mockReport;
    private readonly Mock<IHubContext<ChatHub>>     _mockHub;
    private readonly CommunicationService            _sut;

    public GetOrCreateConversationTests()
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

    private static User U(Guid id, string? first, string? last) =>
        new() { Id = id, FirstName = first ?? "", LastName = last ?? "", Email = "e@e.com" };

    private static Conversation BuildExisting(Guid cid, Guid me, Guid oth) => new()
    {
        Id = cid,
        Type = 1,
        ConversationParticipants = new List<ConversationParticipant>
        {
            new() { UserId = me, User = U(me, "M", "E") },
            new() { UserId = oth, User = U(oth, "O", "2") }
        },
        Messages = new List<Message>()
    };

    // Condition: cùng user.
    [Fact]
    public async Task SameUser_Throws()
    {
        var id = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetOrCreateConversationAsync(id, id));

        Assert.Equal("Khong the tu nhan tin voi chinh minh.", ex.Message);
        _mockRepo.Verify(r => r.FindOneToOneConversationAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    // Condition: đã tồn tại 1-1.
    // Confirmation: map DTO, không tạo mới / không lưu.
    [Fact]
    public async Task Existing_ReturnsMapped_DoesNotCreate()
    {
        var me  = Guid.NewGuid();
        var oth = Guid.NewGuid();
        var cid = Guid.NewGuid();
        var ex  = BuildExisting(cid, me, oth);

        _mockRepo.Setup(r => r.FindOneToOneConversationAsync(me, oth)).ReturnsAsync(ex);

        var dto = await _sut.GetOrCreateConversationAsync(me, oth);

        Assert.Equal(cid, dto.Id);
        Assert.Equal(oth, dto.OtherUserId);
        Assert.Equal("O 2", dto.OtherUserName);
        _mockRepo.Verify(r => r.AddConversation(It.IsAny<Conversation>()), Times.Never);
        _mockRepo.Verify(r => r.AddParticipant(It.IsAny<ConversationParticipant>()), Times.Never);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    // Condition: chưa có, tạo mới.
    // Confirmation: AddConversation, 2 AddParticipant, Save, GetById, DTO hợp lệ.
    [Fact]
    public async Task Create_CallsAddParticipantSave_GetById_Maps()
    {
        var me  = Guid.NewGuid();
        var oth = Guid.NewGuid();
        _mockRepo.Setup(r => r.FindOneToOneConversationAsync(me, oth)).ReturnsAsync((Conversation?)null);

        Conversation? newConv = null;
        _mockRepo.Setup(r => r.AddConversation(It.IsAny<Conversation>()))
            .Callback<Conversation>(c => newConv = c);

        _mockRepo.Setup(r => r.GetConversationByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
            {
                if (newConv == null || id != newConv.Id) return (Conversation?)null;
                return new Conversation
                {
                    Id   = newConv.Id,
                    Type = 1,
                    ConversationParticipants = new List<ConversationParticipant>
                    {
                        new() { UserId = me, User = U(me, "A", "B") },
                        new() { UserId = oth, User = U(oth, "C", "D") }
                    },
                    Messages = new List<Message>()
                };
            });
        _mockRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var dto = await _sut.GetOrCreateConversationAsync(me, oth);

        Assert.Equal(oth, dto.OtherUserId);
        Assert.Equal("C D", dto.OtherUserName);
        Assert.Equal(newConv!.Id, dto.Id);
        _mockRepo.Verify(r => r.AddConversation(It.Is<Conversation>(c => c.Type == 1 && c.CreatedBy == me && c.IsDeleted == false)), Times.Once);
        _mockRepo.Verify(r => r.AddParticipant(It.Is<ConversationParticipant>(p => p.UserId == me && p.IsDeleted == false)), Times.Once);
        _mockRepo.Verify(r => r.AddParticipant(It.Is<ConversationParticipant>(p => p.UserId == oth && p.IsDeleted == false)), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockRepo.Verify(r => r.GetConversationByIdAsync(newConv.Id), Times.Once);
    }
}
