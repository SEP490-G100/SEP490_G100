using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IAccountRepository
{
    Task<(List<User> Users, int TotalCount)> GetPagedModeratorAccountsAsync(
        string? search,
        int? status,
        int page,
        int pageSize);
    Task<User?> GetModeratorAccountWithRolesAsync(Guid id);
    Task<bool> IsEmailInUseAsync(string email);
    Task<Role?> GetRoleByNameAsync(string roleName);
    void AddUser(User user);
    void AddUserRole(UserRole userRole);
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
