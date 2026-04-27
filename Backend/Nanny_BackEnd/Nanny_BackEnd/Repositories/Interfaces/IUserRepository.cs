using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IUserRepository
{
    void Add(User user);
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByIdAsync(Guid id);
    Task<List<string>> GetRolesAsync(Guid userId);
    Task AssignRoleAsync(Guid userId, string roleName);
    Task RemoveAllRolesAsync(Guid userId);
    Task saveChanges();
    Task<(List<User> Users, int TotalCount)> GetPagedUsersAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize,
        string[] excludedRoles,
        string[] allowedRoles);
    Task<User?> GetUserWithRolesAsync(Guid id);
    Task<bool> HasRolesAsync(Guid userId, string[] roleNames);
    Task<List<Guid>> GetActiveUserIdsByRoleAsync(string roleName);
    Task<List<Guid>> GetActiveUserIdsByRolesAsync(IEnumerable<string> roleNames);
    Task<List<string>> GetNotificationAssignableRolesAsync();
    Task<bool> IsEmailInUseAsync(string email);
    Task<bool> IsPhoneInUseAsync(string phoneNumber);
    Task<Role?> GetRoleByNameAsync(string roleName);
    Task<User?> GetUserByIdAndRoleAsync(Guid id, string roleName);
    void AddUserRole(UserRole userRole);
    Task HardDeleteUserAsync(Guid userId);
    Task SaveChangesAsync();
}
