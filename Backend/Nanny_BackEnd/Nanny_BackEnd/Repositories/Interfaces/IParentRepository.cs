using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IParentRepository
{
    Task<ParentProfile?> FindByUserIdAsync(Guid userId);
    Task<ParentProfile?> FindByUserIdWithUserAsync(Guid userId);
    void Add(ParentProfile parentProfile);
    Task SaveChangesAsync();
}
