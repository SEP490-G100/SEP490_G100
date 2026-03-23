using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class VerificationDocument
{
    public Guid Id { get; set; }

    public Guid VerificationRequestId { get; set; }

    public int DocumentType { get; set; }

    public string DocumentUrl { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public int? FileSize { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual VerificationRequest VerificationRequest { get; set; } = null!;
}
