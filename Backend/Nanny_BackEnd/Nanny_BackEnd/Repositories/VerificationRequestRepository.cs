using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class VerificationRequestRepository
{
    private readonly Sep490NannyDbContext _db;

    public VerificationRequestRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<(List<VerificationRequest> Items, int TotalCount)> GetListAsync(
        int? status,
        string? search,
        int page,
        int pageSize)
    {
        var query = _db.VerificationRequests
            .Include(v => v.NannyProfile)
                .ThenInclude(np => np.User)
            .Where(v => !v.IsDeleted)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(v =>
                v.NannyProfile.User.Email.ToLower().Contains(s) ||
                v.NannyProfile.User.FirstName.ToLower().Contains(s) ||
                v.NannyProfile.User.LastName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<VerificationRequest?> GetByIdAsync(Guid id)
    {
        return await _db.VerificationRequests
            .Include(v => v.NannyProfile)
                .ThenInclude(np => np.User)
            .Include(v => v.VerificationDocuments.Where(d => !d.IsDeleted))
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
    }

    public async Task<NannyProfile?> GetNannyProfileAsync(Guid nannyProfileId)
    {
        return await _db.NannyProfiles
            .FirstOrDefaultAsync(np => np.Id == nannyProfileId && !np.IsDeleted);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
