using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class ContractTemplate
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string Version { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public virtual ICollection<HiringRecord> HiringRecords { get; set; } = new List<HiringRecord>();
}
