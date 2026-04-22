using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IModeratorJobRepository
{
    Task<(List<JobPosting> Items, int TotalCount)> ModeratorViewJobListAsync(
        int? status,
        int? moderationStatus,
        string? search,
        int page,
        int pageSize);
    Task<JobPosting?> ModeratorViewJobDetailAsync(Guid jobId);
    Task SaveModeratedJobAsync(JobPosting job);
    Task SaveChangesAsync();
}
