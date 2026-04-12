using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class AdminAccountRepository
{
    private readonly UserRepository _userRepository;

    public AdminAccountRepository(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(List<User> Users, int TotalCount)> GetPagedModeratorAccountsAsync(
        string? search,
        int? status,
        int page,
        int pageSize) =>
        await _userRepository.GetPagedUsersByRoleAsync("Moderator", search, status, page, pageSize);

    public async Task<User?> GetModeratorAccountWithRolesAsync(Guid id) =>
        await _userRepository.GetUserByIdAndRoleAsync(id, "Moderator");

    public async Task<User?> GetUserWithRolesAsync(Guid id) =>
        await _userRepository.GetUserWithRolesAsync(id);

    public async Task<bool> IsEmailInUseAsync(string email) =>
        await _userRepository.IsEmailInUseAsync(email);

    public async Task<Role?> GetRoleByNameAsync(string roleName) =>
        await _userRepository.GetRoleByNameAsync(roleName);

    public void AddUser(User user) =>
        _userRepository.Add(user);

    public void AddUserRole(UserRole userRole) =>
        _userRepository.AddUserRole(userRole);

    public async Task HardDeleteUserAsync(Guid userId) =>
        await _userRepository.HardDeleteUserAsync(userId);

    public async Task SaveChangesAsync() =>
        await _userRepository.saveChanges();
}
