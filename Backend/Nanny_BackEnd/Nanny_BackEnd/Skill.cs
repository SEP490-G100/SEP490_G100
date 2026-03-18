using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class Skill
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<JobRequirement> JobRequirements { get; set; } = new List<JobRequirement>();

    public virtual ICollection<NannySkill> NannySkills { get; set; } = new List<NannySkill>();
}
