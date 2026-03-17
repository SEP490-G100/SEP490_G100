using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class FavoriteNanny
{
    public Guid Id { get; set; }

    public Guid ParentProfileId { get; set; }

    public Guid NannyProfileId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual NannyProfile NannyProfile { get; set; } = null!;

    public virtual ParentProfile ParentProfile { get; set; } = null!;
}
