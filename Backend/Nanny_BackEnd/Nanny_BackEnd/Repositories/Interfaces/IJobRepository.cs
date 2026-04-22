using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IJobRepository
{
    Task<List<JobPosting>> searchJobPosting(
        SearchJobRequest filters,
        Guid? currentUserId = null,
        bool canSeeNannyOnlyJobs = false);
    Task<List<JobPosting>> searchByTitle(string? title);
    Task<List<JobPosting>> getListPosting(Guid parentProfileId);
    Task<JobPosting?> viewDetailPosting(Guid id);
    Task<bool> JobPostingExistsForParentAsync(Guid jobId, Guid parentProfileId);
    Task<ParentProfile?> getParentProfileSnapshot(Guid parentProfileId);
    Task<List<Skill>> getSkillsByNames(IEnumerable<string> names);
    Task<List<Skill>> getActiveSkills();
    void addSkills(IEnumerable<Skill> skills);
    Task<JobPosting> createJobPosting(JobPosting job);
    Task updateJobPosting(JobPosting job);
    Task deleteJobPosting(JobPosting job);
    void removeRequirements(IEnumerable<JobRequirement> reqs);
    void addRequirements(IEnumerable<JobRequirement> reqs);
    void removeScheduleRequirements(IEnumerable<JobScheduleRequirement> reqs);
    void addScheduleRequirements(IEnumerable<JobScheduleRequirement> reqs);
    Task<int> countJobPostingsInCurrentMonth(Guid parentProfileId);
    Task<int> countActiveJobPostings(Guid parentProfileId);
    Task hideExpiredPostings();
    Task<(List<JobPosting> Items, int TotalCount)> GetModeratorJobPostingsAsync(
        int? status,
        int? moderationStatus,
        string? search,
        int page,
        int pageSize);
    Task saveChanges();
}
