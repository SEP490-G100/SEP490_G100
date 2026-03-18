using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class NannySkill
{
    public Guid Id { get; set; }

    public Guid NannyProfileId { get; set; }

    public Guid SkillId { get; set; }

    public int? ProficiencyLevel { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual NannyProfile NannyProfile { get; set; } = null!;

    public virtual Skill Skill { get; set; } = null!;
}
