using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class UserRepository
{
    private readonly Sep490NannyDbContext _db;

    public UserRepository(Sep490NannyDbContext db) => _db = db;

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

    public void Add(User user) => _db.Users.Add(user);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
