using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Nanny_BackEnd.DTOs.Communication;
using Nanny_BackEnd.Services.Interfaces;

namespace Nanny_BackEnd.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ICommunicationService _chatService;

    public ChatHub(ICommunicationService chatService) => _chatService = chatService;

    // ─── Kết nối: thêm user vào group cá nhân ────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        var userId = getCurrentUserId();
        if (userId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, userId.Value.ToString());

        await base.OnConnectedAsync();
    }

    // ─── Ngắt kết nối ─────────────────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = getCurrentUserId();
        if (userId.HasValue)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.Value.ToString());

        await base.OnDisconnectedAsync(exception);
    }

    // ─── Gửi tin nhắn ─────────────────────────────────────────────────────────

    /// <summary>
    /// Client gọi: connection.invoke("SendMessage", convId, content, messageType?)
    /// Server lưu DB rồi broadcast ReceiveMessage đến tất cả participants.
    /// </summary>
    public async Task SendMessage(Guid conversationId, string content, int messageType = 1)
    {
        var senderId = getCurrentUserId();
        if (!senderId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Khong xac dinh duoc nguoi dung.");
            return;
        }

        try
        {
            var dto = new SendMessageDto
            {
                ConversationId = conversationId,
                Content = content,
                MessageType = messageType
            };

            var messageDto = await _chatService.SendMessageAsync(dto, senderId.Value);

            // Broadcast đến tất cả participants (dùng conversation group)
            await Clients.Group(conversationId.ToString())
                .SendAsync("ReceiveMessage", messageDto);
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            await Clients.Caller.SendAsync("Error", "Ban khong co quyen trong cuoc hoi thoai nay.");
        }
        catch (Exception)
        {
            await Clients.Caller.SendAsync("Error", "Loi khi gui tin nhan. Vui long thu lai.");
        }
    }

    // ─── Tham gia group conversation ──────────────────────────────────────────

    /// <summary>Client gọi khi mở một conversation để nhận tin nhắn realtime.</summary>
    public async Task JoinConversation(Guid conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    /// <summary>Client gọi khi đóng/rời conversation.</summary>
    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    // ─── Đánh dấu đã đọc ──────────────────────────────────────────────────────

    /// <summary>Broadcast trạng thái đọc đến conversation group.</summary>
    public async Task MarkRead(Guid conversationId, Guid messageId)
    {
        var userId = getCurrentUserId();
        if (!userId.HasValue) return;

        await Clients.Group(conversationId.ToString())
            .SendAsync("MessageRead", new { messageId, readBy = userId.Value });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private Guid? getCurrentUserId()
    {
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? Context.User?.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
