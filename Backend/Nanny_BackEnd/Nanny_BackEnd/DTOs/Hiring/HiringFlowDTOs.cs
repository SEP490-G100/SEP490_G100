namespace Nanny_BackEnd.DTOs.Hiring;

public class ConfirmHiringDto
{
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class RespondToOfferDto
{
    public string Action { get; set; } = string.Empty;
    public string? DeclineReason { get; set; }
}

public class JobApplicantDto
{
    public Guid JobApplicationId { get; set; }
    public Guid NannyUserId { get; set; }
    public Guid NannyProfileId { get; set; }
    public string NannyName { get; set; } = string.Empty;
    public string? NannyAvatar { get; set; }
    public decimal? NannyRating { get; set; }
    public int? YearsOfExperience { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public DateTime AppliedAt { get; set; }
    public int Status { get; set; }
}

public class HiringOfferDetailDto
{
    public Guid HiringRecordId { get; set; }
    public Guid ContractId { get; set; }
    public Guid JobPostingId { get; set; }
    public string JobPostingTitle { get; set; } = string.Empty;
    public string ParentName { get; set; } = string.Empty;
    public string? ParentAvatar { get; set; }
    public string NannyName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? ContractDuration { get; set; }
    public string ContractContent { get; set; } = string.Empty;
    public bool SignedByParent { get; set; }
    public bool SignedByNanny { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SignedAt { get; set; }
}

public class NannyHireContextDto
{
    public bool CanHire { get; set; }
    public Guid? JobApplicationId { get; set; }
    public Guid? JobPostingId { get; set; }
    public Guid NannyUserId { get; set; }
    public Guid NannyProfileId { get; set; }
    public string NannyName { get; set; } = string.Empty;
    public string? NannyAvatar { get; set; }
}

public class HiringConfirmedDto
{
    public Guid HiringRecordId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid ParentUserId { get; set; }
    public Guid NannyUserId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public int BatchRejectedCount { get; set; }
}
