using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories.Interfaces;

namespace Nanny_BackEnd.Repositories;

public class ContactRequestRepository : IContactRequestRepository
{
    private readonly Sep490NannyDbContext _db;

    public ContactRequestRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<ContactRequest?> FindByParentAndNannyNotDeletedAsync(
        Guid parentProfileId, Guid nannyProfileId) =>
        await _db.ContactRequests
            .FirstOrDefaultAsync(r =>
                r.ParentProfileId == parentProfileId &&
                r.NannyProfileId == nannyProfileId &&
                !r.IsDeleted);

    public void Add(ContactRequest entity) => _db.ContactRequests.Add(entity);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();

    public async Task<(
        List<ContactRequest> Items,
        int Total,
        int Pending,
        int Accepted,
        int Rejected)> GetReceivedListForNannyAsync(Guid nannyProfileId, int? status)
    {
        var baseQuery = _db.ContactRequests
            .Where(r => r.NannyProfileId == nannyProfileId && !r.IsDeleted)
            .Include(r => r.ParentProfile)
            .ThenInclude(p => p.User)
            .AsNoTracking();

        var total = await baseQuery.CountAsync();
        var pending = await baseQuery.CountAsync(r => r.Status == 0);
        var accepted = await baseQuery.CountAsync(r => r.Status == 1);
        var rejected = await baseQuery.CountAsync(r => r.Status == 2);

        var query = baseQuery;
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var items = await query
            .OrderBy(r => r.Status == 0 ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        return (items, total, pending, accepted, rejected);
    }

    public async Task<(
        List<ContactRequest> Items,
        int Total,
        int Pending,
        int Accepted,
        int Rejected)> GetSentListForParentAsync(Guid parentProfileId, int? status)
    {
        var baseQuery = _db.ContactRequests
            .Where(r => r.ParentProfileId == parentProfileId && !r.IsDeleted)
            .Include(r => r.NannyProfile)
            .ThenInclude(n => n.User)
            .AsNoTracking();

        var total = await baseQuery.CountAsync();
        var pending = await baseQuery.CountAsync(r => r.Status == 0);
        var accepted = await baseQuery.CountAsync(r => r.Status == 1);
        var rejected = await baseQuery.CountAsync(r => r.Status == 2);

        var query = baseQuery;
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var items = await query
            .OrderBy(r => r.Status == 0 ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();

        return (items, total, pending, accepted, rejected);
    }

    public async Task<ContactRequest?> GetByIdForDetailNoTrackingAsync(Guid contactRequestId) =>
        await _db.ContactRequests
            .Include(r => r.ParentProfile)
            .ThenInclude(p => p.User)
            .Include(r => r.NannyProfile)
            .ThenInclude(n => n.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == contactRequestId && !r.IsDeleted);

    public async Task<ContactRequest?> GetByIdForNannyReviewTrackingAsync(
        Guid contactRequestId, Guid nannyProfileId) =>
        await _db.ContactRequests
            .Include(r => r.ParentProfile)
            .ThenInclude(p => p.User)
            .Include(r => r.NannyProfile)
            .ThenInclude(n => n.User)
            .FirstOrDefaultAsync(r =>
                r.Id == contactRequestId &&
                !r.IsDeleted &&
                r.NannyProfileId == nannyProfileId);
}
