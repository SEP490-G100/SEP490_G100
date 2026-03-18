using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class JobApplication
{
    public Guid Id { get; set; }

    public Guid JobPostingId { get; set; }

    public Guid NannyProfileId { get; set; }

    public int Status { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? WithdrawnAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<HiringRecord> HiringRecords { get; set; } = new List<HiringRecord>();

    public virtual ICollection<Interview> Interviews { get; set; } = new List<Interview>();

    public virtual JobPosting JobPosting { get; set; } = null!;

    public virtual NannyProfile NannyProfile { get; set; } = null!;
}
