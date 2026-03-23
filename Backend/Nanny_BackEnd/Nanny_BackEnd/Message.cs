using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderUserId { get; set; }

    public string Content { get; set; } = null!;

    public int MessageType { get; set; }

    public string? AttachmentUrl { get; set; }

    public DateTime? ReadAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User SenderUser { get; set; } = null!;
}
