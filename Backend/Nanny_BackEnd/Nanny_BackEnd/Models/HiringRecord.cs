using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class HiringRecord
{
    public Guid Id { get; set; }

    public Guid JobApplicationId { get; set; }

    public Guid ParentProfileId { get; set; }

    public Guid NannyProfileId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? ContractDuration { get; set; }

    public int Status { get; set; }

    public DateTime? ParentConfirmedAt { get; set; }

    public DateTime? NannyConfirmedAt { get; set; }

    public Guid? ContractTemplateId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ContractTemplate? ContractTemplate { get; set; }

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual JobApplication JobApplication { get; set; } = null!;

    public virtual NannyProfile NannyProfile { get; set; } = null!;

    public virtual ParentProfile ParentProfile { get; set; } = null!;

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
