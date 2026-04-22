using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ChildRepository : IChildRepository
{
    private readonly Sep490NannyDbContext _db;

    public ChildRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<List<ChildProfile>> GetByParentProfileIdAsync(Guid parentProfileId) =>
        await _db.ChildProfiles
            .AsNoTracking()
            .Where(c => c.ParentProfileId == parentProfileId && !c.IsDeleted)
            .ToListAsync();

    public async Task<ChildProfile?> FindByIdAndParentAsync(Guid childId, Guid parentProfileId) =>
        await _db.ChildProfiles
            .FirstOrDefaultAsync(c => c.Id == childId && c.ParentProfileId == parentProfileId && !c.IsDeleted);

    public void Add(ChildProfile child) => _db.ChildProfiles.Add(child);

    public void Update(ChildProfile child) => _db.ChildProfiles.Update(child);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}