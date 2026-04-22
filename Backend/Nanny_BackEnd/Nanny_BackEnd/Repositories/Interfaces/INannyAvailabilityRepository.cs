using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface INannyAvailabilityRepository
{
    Task<List<NannyAvailability>> GetByNannyProfileIdAsync(Guid nannyProfileId);
    void AddRange(IEnumerable<NannyAvailability> items);
    void RemoveRange(IEnumerable<NannyAvailability> items);
    Task SaveChangesAsync();
}
