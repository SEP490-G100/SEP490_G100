using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class Review
{
    public Guid Id { get; set; }

    public Guid ReviewerUserId { get; set; }

    public Guid RevieweeUserId { get; set; }

    public Guid HiringRecordId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public bool IsVisible { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual HiringRecord HiringRecord { get; set; } = null!;

    public virtual User RevieweeUser { get; set; } = null!;

    public virtual User ReviewerUser { get; set; } = null!;
}
