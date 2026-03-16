using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class NannyProfileRepository
{
    private readonly Sep490NannyDbContext _db;

    public NannyProfileRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<NannyProfile?> FindByUserIdAsync(Guid userId) =>
        await _db.NannyProfiles.FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

    public void Add(NannyProfile profile) => _db.NannyProfiles.Add(profile);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}

