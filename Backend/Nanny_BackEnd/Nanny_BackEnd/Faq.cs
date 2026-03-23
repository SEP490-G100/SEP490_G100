using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class Faq
{
    public Guid Id { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public string? Category { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public int ViewCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
}
