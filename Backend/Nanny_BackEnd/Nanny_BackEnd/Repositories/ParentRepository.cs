using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ParentRepository : IParentRepository
{
    private readonly Sep490NannyDbContext _db;

    public ParentRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<ParentProfile?> FindByUserIdAsync(Guid userId) =>
        await _db.ParentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

    public async Task<ParentProfile?> FindByUserIdWithUserAsync(Guid userId) =>
        await _db.ParentProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

    public void Add(ParentProfile parentProfile) => _db.ParentProfiles.Add(parentProfile);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}