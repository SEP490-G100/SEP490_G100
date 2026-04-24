using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class Contract
{
    public Guid Id { get; set; }

    public Guid HiringRecordId { get; set; }

    public Guid? ContractTemplateId { get; set; }

    public string? ContractContent { get; set; }

    public bool SignedByParent { get; set; }

    public bool SignedByNanny { get; set; }

    public DateTime? SignedAt { get; set; }

    public string? PdfUrl { get; set; }

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ContractTemplate? ContractTemplate { get; set; }

    public virtual HiringRecord HiringRecord { get; set; } = null!;
}
