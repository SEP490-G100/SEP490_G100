using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class VerificationRequest
{
    public Guid Id { get; set; }

    public Guid NannyProfileId { get; set; }

    public int Status { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual NannyProfile NannyProfile { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual ICollection<VerificationDocument> VerificationDocuments { get; set; } = new List<VerificationDocument>();
}
