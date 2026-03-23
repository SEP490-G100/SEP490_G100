using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int Type { get; set; }

    public bool IsRead { get; set; }

    public Guid? RelatedEntityId { get; set; }

    public string? RelatedEntityType { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual User User { get; set; } = null!;
}
