using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IChildRepository
{
    Task<List<ChildProfile>> GetByParentProfileIdAsync(Guid parentProfileId);
    Task<ChildProfile?> FindByIdAndParentAsync(Guid childId, Guid parentProfileId);
    void Add(ChildProfile child);
    void Update(ChildProfile child);
    Task SaveChangesAsync();
}
