using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class UserRepository
{
    private readonly Sep490NannyDbContext _db;

    public UserRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<User?> findByEmail(string email) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

<<<<<<< HEAD
    public async Task<User?> FindByIdAsync(Guid id) =>
=======
    public async Task<User?> findByGoogleId(string googleId) =>
        await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId && !u.IsDeleted);

    public async Task<User?> findById(Guid id) =>
>>>>>>> bdd254b02b8b7ff6a9a4cde9d5afc77a1b09a9b5
        await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);

    public async Task<List<string>> getRoles(Guid userId) =>
        await _db.UserRoles
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

    public async Task assignRole(Guid userId, string roleName)
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

    public async Task addParentProfile(Guid userId)
    {
        _db.ParentProfiles.Add(new ParentProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
    }

    public async Task saveChanges() => await _db.SaveChangesAsync();
}
