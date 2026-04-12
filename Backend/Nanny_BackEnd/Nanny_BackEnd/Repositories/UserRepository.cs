using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class UserRepository
{
    private readonly Sep490NannyDbContext _db;

    public UserRepository(Sep490NannyDbContext db) => _db = db;

    public void Add(User user) => _db.Users.Add(user);

    public async Task<User?> FindByEmailAsync(string email) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

    public async Task<User?> FindByIdAsync(Guid id) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

    public async Task<List<string>> GetRolesAsync(Guid userId) =>
        await _db.UserRoles
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

    public async Task AssignRoleAsync(Guid userId, string roleName)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName && !r.IsDeleted);
        if (role == null) return;

        _db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = role.Id,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task RemoveAllRolesAsync(Guid userId)
    {
        var roles = await _db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        _db.UserRoles.RemoveRange(roles);
    }

    public async Task saveChanges() => await _db.SaveChangesAsync();

    // ── Moderator / Admin account management ──────────────────────────────

    /// <summary>
    /// Paginated list of users, excluding given roles.
    /// Optionally filter by status, keyword (name/email), and a specific allowed role.
    /// </summary>
    public async Task<(List<User> Users, int TotalCount)> GetPagedUsersAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize,
        string[] excludedRoles,
        string[] allowedRoles)
    {
        var query = _db.Users
            .Where(u => !u.IsDeleted)
            .Where(u => u.UserRoles.Any(ur => !ur.IsDeleted && !excludedRoles.Contains(ur.Role.Name)))
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

        if (!string.IsNullOrWhiteSpace(role) && allowedRoles.Contains(role))
        {
            query = query.Where(u =>
                u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == role));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();

        return (users, totalCount);
    }

    /// <summary>
    /// Find a user by Id with their Roles eagerly loaded.
    /// </summary>
    public async Task<User?> GetUserWithRolesAsync(Guid id) =>
        await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

    /// <summary>
    /// Returns true if the user holds any of the supplied roles.
    /// </summary>
    public async Task<bool> HasRolesAsync(Guid userId, string[] roleNames) =>
        await _db.UserRoles
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .Select(ur => ur.Role.Name)
            .AnyAsync(n => roleNames.Contains(n));

    public async Task<List<Guid>> GetActiveUserIdsByRoleAsync(string roleName) =>
        await _db.Users
            .Where(u => !u.IsDeleted
                && u.Status == 1
                && u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == roleName))
            .Select(u => u.Id)
            .ToListAsync();

    public async Task<List<Guid>> GetActiveUserIdsByRolesAsync(IEnumerable<string> roleNames)
    {
        var normalizedRoles = roleNames
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedRoles.Count == 0)
            return [];

        return await _db.Users
            .Where(u => !u.IsDeleted
                && u.Status == 1
                && u.UserRoles.Any(ur => !ur.IsDeleted && normalizedRoles.Contains(ur.Role.Name)))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync();
    }

    // ── Admin moderator management ─────────────────────────────────────────

    /// <summary>Paginated list of users who have a specific role.</summary>
    public async Task<List<string>> GetNotificationAssignableRolesAsync() =>
        await _db.Roles
            .Where(r => !r.IsDeleted && r.Name != "Admin")
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync();

    public async Task<(List<User> Users, int TotalCount)> GetPagedUsersByRoleAsync(
        string roleName, string? search, int? status, int page, int pageSize)
    {
        var query = _db.Users
            .Where(u => !u.IsDeleted &&
                u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == roleName))
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
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (users, totalCount);
    }

    /// <summary>Check whether an email is already registered.</summary>
    public async Task<bool> IsEmailInUseAsync(string email) =>
        await _db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted);

    /// <summary>Fetch a Role entity by name.</summary>
    public async Task<Role?> GetRoleByNameAsync(string roleName) =>
        await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName && !r.IsDeleted);

    /// <summary>Find a user by Id who must hold a specific role.</summary>
    public async Task<User?> GetUserByIdAndRoleAsync(Guid id, string roleName) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted &&
            u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == roleName));

    public void AddUserRole(UserRole userRole) => _db.UserRoles.Add(userRole);

    /// <summary>
    /// Hard-delete: remove UserRoles first (FK), then the User row itself.
    /// </summary>
    public async Task HardDeleteUserAsync(Guid userId)
    {
        var roles = await _db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
        _db.UserRoles.RemoveRange(roles);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
            _db.Users.Remove(user);
    }
    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
