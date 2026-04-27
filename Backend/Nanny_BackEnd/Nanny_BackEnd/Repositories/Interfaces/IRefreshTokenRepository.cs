using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    void Add(RefreshToken token);
    Task<RefreshToken?> FindByTokenAsync(string token);
    Task RevokeAllForUserAsync(Guid userId);
    Task SaveChangesAsync();
}
