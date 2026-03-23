using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class NannyAvailability
{
    public Guid Id { get; set; }

    public Guid NannyProfileId { get; set; }

    public int DayOfWeek { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public int TimeSlot { get; set; }

    public virtual NannyProfile NannyProfile { get; set; } = null!;
}
