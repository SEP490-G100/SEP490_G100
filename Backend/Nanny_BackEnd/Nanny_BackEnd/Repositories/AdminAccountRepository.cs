using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class AdminAccountRepository
{
    private const string ModeratorRole = "Moderator";
    private readonly Sep490NannyDbContext _db;

    public AdminAccountRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<(List<User> Users, int TotalCount)> GetPagedModeratorAccountsAsync(
        string? search,
        int? status,
        int page,
        int pageSize)
    {
        var query = _db.Users
            .Where(u => u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == ModeratorRole))
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(s) ||
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<User?> GetModeratorAccountWithRolesAsync(Guid id) =>
        await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u =>
                u.Id == id &&
                u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == ModeratorRole));

    public async Task<bool> IsEmailInUseAsync(string email) =>
        await _db.Users.AnyAsync(u => u.Email == email);

    public async Task<Role?> GetRoleByNameAsync(string roleName) =>
        await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName && !r.IsDeleted);

    public void AddUser(User user) =>
        _db.Users.Add(user);

    public void AddUserRole(UserRole userRole) =>
        _db.UserRoles.Add(userRole);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
