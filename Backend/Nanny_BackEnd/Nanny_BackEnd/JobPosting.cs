using System;
using System.Collections.Generic;

namespace Nanny_BackEnd;

public partial class JobPosting
{
    public Guid Id { get; set; }

    public Guid ParentProfileId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int JobType { get; set; }

    public decimal? SalaryMin { get; set; }

    public decimal? SalaryMax { get; set; }

    public int SalaryType { get; set; }

    public bool SalaryNegotiable { get; set; }

    public int? NumberOfChildren { get; set; }

    public string? Location { get; set; }

    public string? City { get; set; }

    public string? District { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int Status { get; set; }

    public int ModerationStatus { get; set; }

    public string? ModerationNote { get; set; }

    public Guid? ModeratedBy { get; set; }

    public DateTime? ModeratedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public int? MinNannyAge { get; set; }

    public int? MaxNannyAge { get; set; }

    public virtual ICollection<FavoriteJobPosting> FavoriteJobPostings { get; set; } = new List<FavoriteJobPosting>();

    public virtual ICollection<JobApplication> JobApplications { get; set; } = new List<JobApplication>();

    public virtual ICollection<JobRequirement> JobRequirements { get; set; } = new List<JobRequirement>();

    public virtual ICollection<JobScheduleRequirement> JobScheduleRequirements { get; set; } = new List<JobScheduleRequirement>();

    public virtual User? ModeratedByNavigation { get; set; }

    public virtual ParentProfile ParentProfile { get; set; } = null!;
}
