using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class Report
{
    public Guid Id { get; set; }

    public Guid ReporterUserId { get; set; }

    public Guid ReportedEntityId { get; set; }

    public string ReportedEntityType { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public string? Evidence { get; set; }

    public int Status { get; set; }

    public Guid? HandledBy { get; set; }

    public DateTime? HandledAt { get; set; }

    public string? Resolution { get; set; }

    public string? ActionTaken { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual User? HandledByNavigation { get; set; }

    public virtual User ReporterUser { get; set; } = null!;
}
