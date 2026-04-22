namespace Nanny_BackEnd.DTOs.Search;

/// <summary>Outcome of nanny applying to a job (mapped to HTTP in SearchController).</summary>
public record ApplyToJobServiceResult(
    bool IsSuccess,
    ApplyToJobFailure? Failure,
    string? ErrorDetailMessage,
    Guid ApplicationId,
    Guid JobPostingId,
    Guid ParentUserId,
    Guid NannyUserId,
    bool IsReapplied,
    string SuccessMessage);

public enum ApplyToJobFailure
{
    NotNanny,
    NotFound,
    JobNotOpen,
    OwnJob,
    AlreadyApplied,
    MonthlyLimit
}

public record NannyMyApplicationItemDto(
    Guid Id,
    Guid JobPostingId,
    string JobTitle,
    string ParentName,
    string? City,
    string? District,
    string? Location,
    int Status,
    string StatusLabel,
    bool CanWithdraw,
    DateTime AppliedAt,
    DateTime? ReviewedAt,
    DateTime? WithdrawnAt);

public record NannyMyApplicationsListResult(
    IReadOnlyList<NannyMyApplicationItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed class ReviewJobApplicationRequestDto
{
    public int Action { get; set; }
    public string? RejectionReason { get; set; }
}

public record ReviewJobApplicationServiceResult(
    bool IsSuccess,
    ReviewJobParentFailure? Failure,
    string? ErrorMessage,
    Guid ApplicationId,
    Guid JobPostingId,
    int Status,
    string? StatusLabel,
    DateTime? ReviewedAt,
    string? RejectionReason,
    Guid ParentUserId,
    Guid NannyUserId,
    string SuccessMessage);

public enum ReviewJobParentFailure
{
    NotParent,
    BadInput,
    ApplicationNotFound,
    Forbidden,
    NannyWithdrawn,
    AlreadyProcessed,
    NotPending
}

public record ParentJobSummaryDto(
    Guid Id,
    string Title,
    int Status,
    int ModerationStatus,
    string? City,
    string? District,
    string? Location,
    DateTime? CreatedAt,
    DateTime? PublishedAt,
    DateTime? ExpiresAt);

public record ParentNannyInApplicationDto(
    Guid ProfileId,
    Guid? UserId,
    string FullName,
    string? AvatarUrl,
    string? PhoneNumber,
    string? City,
    string? District,
    int? YearsOfExperience,
    decimal? ExpectedSalaryMin,
    decimal? ExpectedSalaryMax);

public record ParentApplicationRowDto(
    Guid Id,
    int Status,
    string StatusLabel,
    DateTime AppliedAt,
    DateTime? ReviewedAt,
    DateTime? WithdrawnAt,
    string? RejectionReason,
    bool CanReview,
    ParentNannyInApplicationDto Nanny);

public record ParentJobApplicationsListResult(
    ParentJobSummaryDto Job,
    int TotalApplications,
    int PendingApplications,
    IReadOnlyList<ParentApplicationRowDto> Applications);

public enum GetParentJobApplicationsFailure
{
    NotParent,
    JobNotFound,
    InvalidStatusFilter
}

public record WithdrawApplicationFailureResult(
    bool IsSuccess,
    WithdrawForNannyFailure? Failure,
    string? ErrorMessage);

public enum WithdrawForNannyFailure
{
    NotNanny,
    NotFound,
    NotPending
}
