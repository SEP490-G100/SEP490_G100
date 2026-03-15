using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class NannyAvailabilityRepository
{
    private readonly Sep490NannyDbContext _db;

    public NannyAvailabilityRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<List<NannyAvailability>> GetByNannyProfileIdAsync(Guid nannyProfileId) =>
        await _db.NannyAvailabilities
            .Where(a => a.NannyProfileId == nannyProfileId && !a.IsDeleted)
            .ToListAsync();

    public void AddRange(IEnumerable<NannyAvailability> items) => _db.NannyAvailabilities.AddRange(items);

    public void RemoveRange(IEnumerable<NannyAvailability> items) => _db.NannyAvailabilities.RemoveRange(items);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}

