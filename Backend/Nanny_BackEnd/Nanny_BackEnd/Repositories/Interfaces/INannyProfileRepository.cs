using Nanny_BackEnd.DTOs.Nanny;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface INannyProfileRepository
{
    Task<NannyProfile?> FindByUserIdAsync(Guid userId);
    Task<NannyProfile?> FindByIdWithUserAsync(Guid nannyProfileId);
    IQueryable<NannyProfile> GetSearchQuery();
    Task<(List<NannyProfile> Items, int TotalCount)> SearchAsync(
        NannyListRequest request,
        IEnumerable<Guid> skillIds);
    Task<NannyProfile?> GetDetailAsync(Guid nannyProfileId);
    void Add(NannyProfile profile);
    Task SaveChangesAsync();
}
