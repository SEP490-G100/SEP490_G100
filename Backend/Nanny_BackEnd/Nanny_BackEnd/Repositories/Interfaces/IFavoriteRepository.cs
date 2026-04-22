using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IFavoriteRepository
{
    Task<bool> isFavoriteJob(Guid nannyProfileId, Guid jobPostingId);
    Task addFavoriteJob(Guid nannyProfileId, Guid jobPostingId);
    Task<bool> toggleFavoriteJob(Guid nannyProfileId, Guid jobPostingId, Guid? actorUserId = null);
    Task<HashSet<Guid>> getFavoriteJobIds(Guid nannyProfileId, IEnumerable<Guid> jobPostingIds);
    Task<bool> isFavoriteNanny(Guid parentProfileId, Guid nannyProfileId);
    Task<bool> toggleFavoriteNanny(Guid parentProfileId, Guid nannyProfileId, Guid? actorUserId = null);
    Task<HashSet<Guid>> getFavoriteNannyIds(Guid parentProfileId, IEnumerable<Guid> nannyProfileIds);
    Task<(List<JobPosting> Items, int TotalCount)> getFavoriteJobs(Guid nannyProfileId, int page, int pageSize);
    Task<(List<NannyProfile> Items, int TotalCount)> getFavoriteNannies(Guid parentProfileId, int page, int pageSize);
}
