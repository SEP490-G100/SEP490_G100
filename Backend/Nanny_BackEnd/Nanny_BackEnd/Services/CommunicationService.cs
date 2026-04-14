using Nanny_BackEnd.DTOs.Communication;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class CommunicationService
{
    private readonly CommunicationRepository _repo;
    private readonly NotificationService _notificationService;
    private readonly UserRepository _userRepo;
    private readonly ReportService _reportService;

    public CommunicationService(
        CommunicationRepository repo,
        NotificationService notificationService,
        UserRepository userRepo,
        ReportService reportService)
    {
        _repo = repo;
        _notificationService = notificationService;
        _userRepo = userRepo;
        _reportService = reportService;
    }

    // ─── Lấy danh sách hội thoại ─────────────────────────────────────────────

    public async Task<List<ConversationDto>> GetConversationsAsync(Guid userId)
    {
        var conversations = await _repo.GetConversationsByUserIdAsync(userId);
        return conversations.Select(c => mapConversation(c, userId)).ToList();
    }

    // ─── Lấy lịch sử tin nhắn (phân trang) ──────────────────────────────────

    public async Task<MessageHistoryDto> GetMessageHistoryAsync(
        Guid conversationId, Guid currentUserId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var conversation = await _repo.GetConversationByIdAsync(conversationId)
            ?? throw new KeyNotFoundException("Khong tim thay cuoc hoi thoai.");

        var myParticipant = conversation.ConversationParticipants
            .FirstOrDefault(p => p.UserId == currentUserId)
            ?? throw new UnauthorizedAccessException("Ban khong phai thanh vien cua cuoc hoi thoai nay.");

        var otherParticipant = conversation.ConversationParticipants
            .FirstOrDefault(p => p.UserId != currentUserId);

        var (messages, total) = await _repo.GetMessagesByConversationIdAsync(
            conversationId,
            skip: (page - 1) * pageSize,
            take: pageSize);

        return new MessageHistoryDto
        {
            ConversationId = conversationId,
            OtherUserName = getDisplayName(otherParticipant?.User),
            OtherUserId = otherParticipant?.UserId ?? Guid.Empty,
            IsBlocked = myParticipant.IsBlocked,
            BlockedByUserId = myParticipant.IsBlocked ? myParticipant.UpdatedBy : null,
            IsHidden = myParticipant.IsHidden,
            Messages = messages.Select(m => mapMessage(m, currentUserId)).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ─── Tạo hoặc lấy conversation 1-1 ──────────────────────────────────────

    public async Task<ConversationDto> GetOrCreateConversationAsync(
        Guid currentUserId, Guid otherUserId)
    {
        if (currentUserId == otherUserId)
            throw new InvalidOperationException("Khong the tu nhan tin voi chinh minh.");

        var existing = await _repo.FindOneToOneConversationAsync(currentUserId, otherUserId);
        if (existing != null)
            return mapConversation(existing, currentUserId);

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = 1,
            CreatedAt = now,
            CreatedBy = currentUserId,
            IsDeleted = false
        };
        _repo.AddConversation(conversation);

        _repo.AddParticipant(new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            UserId = currentUserId,
            JoinedAt = now,
            CreatedAt = now,
            CreatedBy = currentUserId,
            IsDeleted = false
        });

        _repo.AddParticipant(new ConversationParticipant
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            UserId = otherUserId,
            JoinedAt = now,
            CreatedAt = now,
            CreatedBy = currentUserId,
            IsDeleted = false
        });

        await _repo.SaveChangesAsync();

        var created = await _repo.GetConversationByIdAsync(conversation.Id)!;
        return mapConversation(created!, currentUserId);
    }

    // ─── Gửi tin nhắn ────────────────────────────────────────────────────────

    public async Task<MessageDto> SendMessageAsync(SendMessageDto dto, Guid senderId)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new ArgumentException("Noi dung tin nhan khong duoc de trong.");

        var myParticipant = await _repo.GetParticipantAsync(dto.ConversationId, senderId)
            ?? throw new UnauthorizedAccessException("Ban khong phai thanh vien cua cuoc hoi thoai nay.");

        if (myParticipant.IsBlocked)
            throw new InvalidOperationException("Cuoc hoi thoai nay dang bi chan. Ban khong the gui tin nhan.");

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = dto.ConversationId,
            SenderUserId = senderId,
            Content = dto.Content.Trim(),
            MessageType = dto.MessageType,
            AttachmentUrl = dto.AttachmentUrl,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _repo.AddMessage(message);

        // Cập nhật LastMessageAt trên conversation
        var conversation = await _repo.GetConversationByIdAsync(dto.ConversationId);
        if (conversation != null)
        {
            conversation.LastMessageAt = message.CreatedAt;
            conversation.UpdatedAt = message.CreatedAt;
            conversation.UpdatedBy = senderId;
        }

        await _repo.SaveChangesAsync();

        // Load lại message với SenderUser
        var saved = await _repo.GetMessageByIdAsync(message.Id) ?? message;
        saved.SenderUser = myParticipant.User ?? new User { Id = senderId, FirstName = "Nguoi", LastName = "Dung", Email = "" };

        var participants = await _repo.GetParticipantsByConversationIdAsync(dto.ConversationId);
        var moderatorRecipientIds = new List<Guid>();
        foreach (var participant in participants.Where(p => p.UserId != senderId))
        {
            if (await _userRepo.HasRolesAsync(participant.UserId, ["Moderator"]))
                moderatorRecipientIds.Add(participant.UserId);
        }

        if (moderatorRecipientIds.Count > 0)
        {
            await _notificationService.createNotificationForUsers(
                moderatorRecipientIds,
                "Co tin nhan moi gui toi moderator",
                $"{getDisplayName(saved.SenderUser)} vua gui mot tin nhan moi can moderator phan hoi.",
                NotificationTypes.MessageToModerator,
                dto.ConversationId,
                "Conversation",
                senderId);
        }

        return mapMessage(saved, senderId);
    }

    // ─── Xóa tin nhắn (soft delete) ──────────────────────────────────────────

    public async Task<(Guid ConversationId, Guid MessageId)> DeleteMessageAsync(Guid messageId, Guid userId)
    {
        var message = await _repo.GetMessageByIdAsync(messageId)
            ?? throw new KeyNotFoundException("Khong tim thay tin nhan.");

        if (message.SenderUserId != userId)
            throw new UnauthorizedAccessException("Ban khong co quyen xoa tin nhan nay.");

        message.IsDeleted = true;
        message.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync();

        return (message.ConversationId, message.Id);
    }

    // ─── Báo cáo tin nhắn ────────────────────────────────────────────────────

    public async Task ReportMessageAsync(Guid messageId, Guid reporterUserId, ReportMessageDto dto)
    {
        var message = await _repo.GetMessageByIdAsync(messageId)
            ?? throw new KeyNotFoundException("Khong tim thay tin nhan.");

        if (message.SenderUserId == reporterUserId)
            throw new InvalidOperationException("Ban khong the bao cao tin nhan cua chinh minh.");

        _repo.AddReport(new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = reporterUserId,
            ReportedEntityId = messageId,
            ReportedEntityType = "Message",
            Reason = dto.Reason,
            Evidence = dto.Evidence,
            Status = 0, // Pending
            CreatedAt = DateTime.UtcNow,
            CreatedBy = reporterUserId,
            IsDeleted = false
        });

        await _repo.SaveChangesAsync();
    }

    // ─── Cập nhật trạng thái hội thoại (Block/Hide) ──────────────────────────

    public async Task<(bool IsBlocked, bool IsHidden, Guid? BlockedByUserId)> UpdateConversationStatusAsync(
        Guid conversationId, Guid userId, UpdateConversationStatusDto dto)
    {
        var participant = await _repo.GetParticipantAsync(conversationId, userId)
            ?? throw new UnauthorizedAccessException("Ban khong phai thanh vien cua cuoc hoi thoai nay.");

        var participants = await _repo.GetParticipantsByConversationIdAsync(conversationId);
        var now = DateTime.UtcNow;
        switch (dto.Action.ToLower())
        {
            case "block":
                foreach (var p in participants)
                {
                    p.IsBlocked = true;
                    p.UpdatedAt = now;
                    p.UpdatedBy = userId;
                }
                break;
            case "unblock":
                if (participant.UpdatedBy != userId)
                    throw new InvalidOperationException("Chi nguoi da chan cuoc tro chuyen moi co quyen mo chan.");

                foreach (var p in participants)
                {
                    p.IsBlocked = false;
                    p.UpdatedAt = now;
                    p.UpdatedBy = userId;
                }
                break;
            default:
                throw new ArgumentException($"Hanh dong '{dto.Action}' khong hop le. Cac gia tri hop le: block, unblock.");
        }

        participant.UpdatedAt = now;
        participant.UpdatedBy = userId;
        await _repo.SaveChangesAsync();

        return (participant.IsBlocked, participant.IsHidden, participant.IsBlocked ? participant.UpdatedBy : null);
    }

    // ─── Mapping helpers ─────────────────────────────────────────────────────

    private static ConversationDto mapConversation(Conversation c, Guid currentUserId)
    {
        var myParticipant = c.ConversationParticipants.FirstOrDefault(p => p.UserId == currentUserId);
        var otherParticipant = c.ConversationParticipants.FirstOrDefault(p => p.UserId != currentUserId);
        var lastMsg = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

        return new ConversationDto
        {
            Id = c.Id,
            OtherUserId = otherParticipant?.UserId ?? Guid.Empty,
            OtherUserName = getDisplayName(otherParticipant?.User),
            OtherUserAvatar = otherParticipant?.User?.AvatarUrl,
            LastMessage = lastMsg?.IsDeleted == true ? "[Tin nhan da bi xoa]" : lastMsg?.Content,
            LastMessageAt = c.LastMessageAt,
            IsBlocked = myParticipant?.IsBlocked ?? false,
            IsHidden = myParticipant?.IsHidden ?? false,
            UnreadCount = c.Messages.Count(m => !m.IsDeleted && m.ReadAt == null && m.SenderUserId != currentUserId)
        };
    }

    private static MessageDto mapMessage(Message m, Guid currentUserId) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderId = m.SenderUserId,
        SenderName = getDisplayName(m.SenderUser),
        SenderAvatar = m.SenderUser?.AvatarUrl,
        Content = m.IsDeleted ? "[Tin nhan da bi xoa]" : m.Content,
        MessageType = m.MessageType,
        AttachmentUrl = m.IsDeleted ? null : m.AttachmentUrl,
        ReadAt = m.ReadAt,
        CreatedAt = m.CreatedAt,
        IsDeleted = m.IsDeleted,
        IsMine = m.SenderUserId == currentUserId
    };

    private static string getDisplayName(User? user)
    {
        if (user == null) return "Nguoi dung";
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? user.Email : name;
    }
}
