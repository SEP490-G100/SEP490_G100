using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class ParentProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? FamilyDescription { get; set; }

    public int? NumberOfChildren { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<ChildProfile> ChildProfiles { get; set; } = new List<ChildProfile>();

    public virtual ICollection<ContactRequest> ContactRequests { get; set; } = new List<ContactRequest>();

    public virtual ICollection<FavoriteNanny> FavoriteNannies { get; set; } = new List<FavoriteNanny>();

    public virtual ICollection<HiringRecord> HiringRecords { get; set; } = new List<HiringRecord>();

    public virtual ICollection<Interview> Interviews { get; set; } = new List<Interview>();

    public virtual ICollection<JobPosting> JobPostings { get; set; } = new List<JobPosting>();

    public virtual User User { get; set; } = null!;
}
