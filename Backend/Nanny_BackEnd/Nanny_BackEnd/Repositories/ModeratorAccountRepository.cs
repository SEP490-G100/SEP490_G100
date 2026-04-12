using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ModeratorAccountRepository
{
    private readonly UserRepository _userRepository;

    public ModeratorAccountRepository(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(List<User> Users, int TotalCount)> GetPagedAccountsAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize,
        string[] excludedRoles,
        string[] allowedRoles) =>
        await _userRepository.GetPagedUsersAsync(role, status, search, page, pageSize, excludedRoles, allowedRoles);

    public async Task<User?> GetAccountWithRolesAsync(Guid id) =>
        await _userRepository.GetUserWithRolesAsync(id);

    public async Task<User?> FindByIdAsync(Guid id) =>
        await _userRepository.FindByIdAsync(id);

    public async Task SaveChangesAsync() =>
        await _userRepository.saveChanges();
}
