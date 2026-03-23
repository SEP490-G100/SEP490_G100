using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class Conversation
{
    public Guid Id { get; set; }

    public int Type { get; set; }

    public string? Title { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
}
