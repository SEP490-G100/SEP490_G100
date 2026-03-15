using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class ChildProfile
{
    public Guid Id { get; set; }

    public Guid ParentProfileId { get; set; }

    public string? Name { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public int? Gender { get; set; }

    public string? SpecialNeeds { get; set; }

    public string? Allergies { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ParentProfile ParentProfile { get; set; } = null!;
}
