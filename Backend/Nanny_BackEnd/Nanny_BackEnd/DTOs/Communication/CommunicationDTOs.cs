namespace Nanny_BackEnd.DTOs.Communication;

// ─── Response DTOs ────────────────────────────────────────────────────────────

public class ConversationDto
{
    public Guid Id { get; set; }
    public string OtherUserName { get; set; } = null!;
    public string? OtherUserAvatar { get; set; }
    public Guid OtherUserId { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsHidden { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = null!;
    public string? SenderAvatar { get; set; }
    public string Content { get; set; } = null!;
    public int MessageType { get; set; }
    public string? AttachmentUrl { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsMine { get; set; }
}

public class MessageHistoryDto
{
    public Guid ConversationId { get; set; }
    public string OtherUserName { get; set; } = null!;
    public Guid OtherUserId { get; set; }
    public bool IsBlocked { get; set; }
    public Guid? BlockedByUserId { get; set; }
    public bool IsHidden { get; set; }
    public List<MessageDto> Messages { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public class SendMessageDto
{
    public Guid ConversationId { get; set; }
    public string Content { get; set; } = null!;
    public int MessageType { get; set; } = 1; // 1 = Text, 2 = Image, 3 = File
    public string? AttachmentUrl { get; set; }
}

public class GetOrCreateConversationDto
{
    public Guid OtherUserId { get; set; }
}

public class ReportMessageDto
{
    public string Reason { get; set; } = null!;
    public string? Evidence { get; set; }
}

public class UpdateConversationStatusDto
{
    /// <summary>"block" | "unblock"</summary>
    public string Action { get; set; } = null!;
}
