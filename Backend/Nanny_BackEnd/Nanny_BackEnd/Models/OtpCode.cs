using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class OtpCode
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string Code { get; set; } = null!;

    public int Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual User? User { get; set; }
}
