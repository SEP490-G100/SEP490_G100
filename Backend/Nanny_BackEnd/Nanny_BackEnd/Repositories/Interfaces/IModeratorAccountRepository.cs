using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IModeratorAccountRepository
{
    Task<(List<User> Users, int TotalCount)> GetPagedAccountsAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize,
        string[] excludedRoles,
        string[] allowedRoles);
    Task<User?> GetAccountWithRolesAsync(Guid id);
    Task<User?> FindByIdAsync(Guid id);
    Task SaveChangesAsync();
}
