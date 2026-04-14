using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class FavoriteRepository
{
    private readonly Sep490NannyDbContext _db;

    public FavoriteRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<bool> isFavoriteJob(Guid nannyProfileId, Guid jobPostingId) =>
        await _db.FavoriteJobPostings.AnyAsync(f =>
            f.NannyProfileId == nannyProfileId &&
            f.JobPostingId == jobPostingId &&
            !f.IsDeleted);

    public async Task addFavoriteJob(Guid nannyProfileId, Guid jobPostingId)
    {
        _db.FavoriteJobPostings.Add(new FavoriteJobPosting
        {
            Id = Guid.NewGuid(),
            NannyProfileId = nannyProfileId,
            JobPostingId = jobPostingId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        });
        await _db.SaveChangesAsync();
    }

    public async Task<bool> toggleFavoriteJob(Guid nannyProfileId, Guid jobPostingId, Guid? actorUserId = null)
    {
        var nowUtc = DateTime.UtcNow;
        var record = await _db.FavoriteJobPostings
            .FirstOrDefaultAsync(f => f.NannyProfileId == nannyProfileId && f.JobPostingId == jobPostingId);

        if (record == null)
        {
            _db.FavoriteJobPostings.Add(new FavoriteJobPosting
            {
                Id = Guid.NewGuid(),
                NannyProfileId = nannyProfileId,
                JobPostingId = jobPostingId,
                CreatedAt = nowUtc,
                CreatedBy = actorUserId,
                IsDeleted = false
            });
            await _db.SaveChangesAsync();
            return true;
        }

        record.IsDeleted = !record.IsDeleted;
        record.UpdatedAt = nowUtc;
        record.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync();
        return !record.IsDeleted;
    }

    public async Task<HashSet<Guid>> getFavoriteJobIds(Guid nannyProfileId, IEnumerable<Guid> jobPostingIds)
    {
        var targetIds = jobPostingIds.Distinct().ToList();
        if (targetIds.Count == 0) return [];

        var ids = await _db.FavoriteJobPostings
            .Where(f => f.NannyProfileId == nannyProfileId && !f.IsDeleted && targetIds.Contains(f.JobPostingId))
            .Select(f => f.JobPostingId)
            .ToListAsync();

        return ids.ToHashSet();
    }

    public async Task<bool> isFavoriteNanny(Guid parentProfileId, Guid nannyProfileId) =>
        await _db.FavoriteNannies.AnyAsync(f =>
            f.ParentProfileId == parentProfileId &&
            f.NannyProfileId == nannyProfileId &&
            !f.IsDeleted);

    public async Task<bool> toggleFavoriteNanny(Guid parentProfileId, Guid nannyProfileId, Guid? actorUserId = null)
    {
        var nowUtc = DateTime.UtcNow;
        var record = await _db.FavoriteNannies
            .FirstOrDefaultAsync(f => f.ParentProfileId == parentProfileId && f.NannyProfileId == nannyProfileId);

        if (record == null)
        {
            _db.FavoriteNannies.Add(new FavoriteNanny
            {
                Id = Guid.NewGuid(),
                ParentProfileId = parentProfileId,
                NannyProfileId = nannyProfileId,
                CreatedAt = nowUtc,
                CreatedBy = actorUserId,
                IsDeleted = false
            });
            await _db.SaveChangesAsync();
            return true;
        }

        record.IsDeleted = !record.IsDeleted;
        record.UpdatedAt = nowUtc;
        record.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync();
        return !record.IsDeleted;
    }

    public async Task<HashSet<Guid>> getFavoriteNannyIds(Guid parentProfileId, IEnumerable<Guid> nannyProfileIds)
    {
        var targetIds = nannyProfileIds.Distinct().ToList();
        if (targetIds.Count == 0) return [];

        var ids = await _db.FavoriteNannies
            .Where(f => f.ParentProfileId == parentProfileId && !f.IsDeleted && targetIds.Contains(f.NannyProfileId))
            .Select(f => f.NannyProfileId)
            .ToListAsync();

        return ids.ToHashSet();
    }

    public async Task<(List<JobPosting> Items, int TotalCount)> getFavoriteJobs(Guid nannyProfileId, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 12 : Math.Min(pageSize, 50);

        var favoriteJobIdsQuery = _db.FavoriteJobPostings
            .AsNoTracking()
            .Where(f =>
                f.NannyProfileId == nannyProfileId &&
                !f.IsDeleted &&
                !f.JobPosting.IsDeleted &&
                f.JobPosting.ModerationStatus == (int)JobPostingModerationStatus.Approved &&
                f.JobPosting.Status == (int)JobPostingStatus.Public)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.JobPostingId);

        var totalCount = await favoriteJobIdsQuery.CountAsync();
        var pageJobIds = await favoriteJobIdsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (pageJobIds.Count == 0)
            return (new List<JobPosting>(), totalCount);

        var jobOrder = pageJobIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        var jobs = await _db.JobPostings
            .AsNoTracking()
            .Where(j => pageJobIds.Contains(j.Id) && !j.IsDeleted)
            .Include(j => j.ChildProfile)
            .Include(j => j.JobRequirements.Where(jr => !jr.IsDeleted)).ThenInclude(jr => jr.Skill)
            .Include(j => j.JobScheduleRequirements.Where(js => !js.IsDeleted))
            .Include(j => j.ParentProfile).ThenInclude(p => p.User)
            .ThenInclude(u => u.UserSubscriptions)
            .ThenInclude(s => s.SubscriptionPlan)
            .Include(j => j.ParentProfile).ThenInclude(p => p.ChildProfiles.Where(c => !c.IsDeleted))
            .ToListAsync();

        var ordered = jobs
            .Where(job => jobOrder.ContainsKey(job.Id))
            .OrderBy(job => jobOrder[job.Id])
            .ToList();

        return (ordered, totalCount);
    }

    public async Task<(List<NannyProfile> Items, int TotalCount)> getFavoriteNannies(Guid parentProfileId, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 12 : Math.Min(pageSize, 50);

        var favoriteNannyIdsQuery = _db.FavoriteNannies
            .AsNoTracking()
            .Where(f =>
                f.ParentProfileId == parentProfileId &&
                !f.IsDeleted &&
                !f.NannyProfile.IsDeleted &&
                !f.NannyProfile.User.IsDeleted &&
                f.NannyProfile.User.Status == (int)UserStatus.Active)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.NannyProfileId);

        var totalCount = await favoriteNannyIdsQuery.CountAsync();
        var pageNannyIds = await favoriteNannyIdsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (pageNannyIds.Count == 0)
            return (new List<NannyProfile>(), totalCount);

        var nannyOrder = pageNannyIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        var nannies = await _db.NannyProfiles
            .AsNoTracking()
            .Where(n => pageNannyIds.Contains(n.Id) && !n.IsDeleted && !n.User.IsDeleted && n.User.Status == (int)UserStatus.Active)
            .Include(n => n.User)
                .ThenInclude(u => u.UserSubscriptions)
                    .ThenInclude(s => s.SubscriptionPlan)
            .Include(n => n.NannySkills.Where(s => !s.IsDeleted && !s.Skill.IsDeleted && s.Skill.IsActive))
                .ThenInclude(s => s.Skill)
            .Include(n => n.NannyAvailabilities.Where(a => !a.IsDeleted && a.IsAvailable))
            .ToListAsync();

        var ordered = nannies
            .Where(nanny => nannyOrder.ContainsKey(nanny.Id))
            .OrderBy(nanny => nannyOrder[nanny.Id])
            .ToList();

        return (ordered, totalCount);
    }
}
