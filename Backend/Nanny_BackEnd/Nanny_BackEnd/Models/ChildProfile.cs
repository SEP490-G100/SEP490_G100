using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class ChildProfile
{
    public Guid Id { get; set; }

    public Guid ParentProfileId { get; set; }

    public string? SpecialNeeds { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public string? Characteristic { get; set; }

    public byte? ChildAgeGroup { get; set; }

    public virtual ICollection<JobPosting> JobPostings { get; set; } = new List<JobPosting>();

    public virtual ParentProfile ParentProfile { get; set; } = null!;
}
