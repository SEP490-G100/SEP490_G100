using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IJobApplicationRepository
{
    Task<NannyProfile?> GetNannyProfileWithUserAsync(Guid userId);
    Task<Guid?> GetNannyProfileIdByUserIdAsync(Guid userId);
    Task<bool> HasApprovedHealthCertificateAsync(Guid nannyProfileId, DateTime utcNow);
    Task<JobPosting?> GetJobPostingForApplyAsync(Guid jobPostingId);
    Task<JobPosting?> GetJobPostingForParentAsync(Guid jobPostingId, Guid parentProfileId);
    Task<JobApplication?> GetExistingApplicationAsync(Guid jobPostingId, Guid nannyProfileId);
    Task<int> CountMonthlyApplicationsAsync(Guid nannyProfileId, DateTime startOfMonth);
    void AddApplication(JobApplication application);
    Task<(List<JobApplication> Items, int Total)> GetPagedApplicationsForNannyAsync(
        Guid nannyProfileId, int skip, int take);
    Task<JobApplication?> GetApplicationForWithdrawAsync(
        Guid applicationId, Guid nannyProfileId);
    Task<JobApplication?> GetApplicationForReviewAsync(Guid applicationId);
    Task<Guid?> GetParentProfileIdByUserIdAsync(Guid userId);
    Task<List<JobApplication>> GetApplicationsByJobPostingAsync(
        Guid jobPostingId, int? status);
    Task SaveChangesAsync();
}
