using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ModeratorAccountRepository
{
    private readonly Sep490NannyDbContext _db;

    public ModeratorAccountRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<(List<User> Users, int TotalCount)> GetPagedAccountsAsync(
        string? role,
        int? status,
        string? search,
        int page,
        int pageSize,
        string[] excludedRoles,
        string[] allowedRoles)
    {
        var query = _db.Users
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

    public async Task<User?> GetAccountWithRolesAsync(Guid id) =>
        await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User?> FindByIdAsync(Guid id) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task SaveChangesAsync() =>
        await _db.SaveChangesAsync();
}
