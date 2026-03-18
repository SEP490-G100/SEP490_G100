using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class JobRequirement
{
    public Guid Id { get; set; }

    public Guid JobPostingId { get; set; }

    public Guid SkillId { get; set; }

    public bool IsRequired { get; set; }

    public int? MinProficiencyLevel { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual JobPosting JobPosting { get; set; } = null!;

    public virtual Skill Skill { get; set; } = null!;
}
