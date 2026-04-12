using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ModeratorJobRepository
{
    private readonly JobRepository _jobRepository;

    public ModeratorJobRepository(JobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<(List<JobPosting> Items, int TotalCount)> ModeratorViewJobListAsync(
        int? status,
        int? moderationStatus,
        string? search,
        int page,
        int pageSize) =>
        await _jobRepository.GetModeratorJobPostingsAsync(status, moderationStatus, search, page, pageSize);

    public async Task<JobPosting?> ModeratorViewJobDetailAsync(Guid jobId) =>
        await _jobRepository.viewDetailPosting(jobId);

    public async Task SaveModeratedJobAsync(JobPosting job) =>
        await _jobRepository.updateJobPosting(job);

    public async Task SaveChangesAsync() =>
        await _jobRepository.saveChanges();
}
