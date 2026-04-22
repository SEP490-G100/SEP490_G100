using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IAdminAccountRepository
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
    Task SaveChangesAsync();
}
