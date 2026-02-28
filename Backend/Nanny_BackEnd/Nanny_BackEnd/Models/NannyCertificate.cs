using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class NannyCertificate
{
    public Guid Id { get; set; }

    public Guid NannyProfileId { get; set; }

    public string Name { get; set; } = null!;

    public string? IssuingOrganization { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? CertificateUrl { get; set; }

    public int VerificationStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual NannyProfile NannyProfile { get; set; } = null!;
}
