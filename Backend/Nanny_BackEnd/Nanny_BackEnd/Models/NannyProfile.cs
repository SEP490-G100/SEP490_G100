using System;
using System.Collections.Generic;

namespace Nanny_BackEnd.Models;

public partial class NannyProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? Bio { get; set; }

    public int? YearsOfExperience { get; set; }

    public int? EducationLevel { get; set; }

    public decimal? ExpectedSalaryMin { get; set; }

    public decimal? ExpectedSalaryMax { get; set; }

    public int SalaryType { get; set; }

    public int? MaxTravelDistance { get; set; }

    public int ProfileCompleteness { get; set; }

    public int VerificationStatus { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public Guid? VerifiedBy { get; set; }

    public decimal? AverageRating { get; set; }

    public int TotalReviews { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public string? Embedding { get; set; }

    public DateTime? EmbeddingUpdatedAt { get; set; }

    public virtual ICollection<ContactRequest> ContactRequests { get; set; } = new List<ContactRequest>();

    public virtual ICollection<FavoriteJobPosting> FavoriteJobPostings { get; set; } = new List<FavoriteJobPosting>();

    public virtual ICollection<FavoriteNanny> FavoriteNannies { get; set; } = new List<FavoriteNanny>();

    public virtual ICollection<HiringRecord> HiringRecords { get; set; } = new List<HiringRecord>();

    public virtual ICollection<Interview> Interviews { get; set; } = new List<Interview>();

    public virtual ICollection<JobApplication> JobApplications { get; set; } = new List<JobApplication>();

    public virtual ICollection<NannyAvailability> NannyAvailabilities { get; set; } = new List<NannyAvailability>();

    public virtual ICollection<NannyCertificate> NannyCertificates { get; set; } = new List<NannyCertificate>();

    public virtual ICollection<NannySkill> NannySkills { get; set; } = new List<NannySkill>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<VerificationRequest> VerificationRequests { get; set; } = new List<VerificationRequest>();

    public virtual User? VerifiedByNavigation { get; set; }
}
