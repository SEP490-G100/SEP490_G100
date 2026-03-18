using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class JobRepository
{
    private readonly Sep490NannyDbContext _db;

    public JobRepository(Sep490NannyDbContext db) => _db = db;


    public async Task<List<JobPosting>> searchJobPosting(
        SearchJobRequest filters,
        Guid? currentUserId = null,
        bool canSeeNannyOnlyJobs = false)
    {
        var nowUtc = DateTime.UtcNow;
        var query = _db.JobPostings
            .Where(j => !j.IsDeleted)
            .Include(j => j.ChildProfile)
            .Include(j => j.JobRequirements.Where(jr => !jr.IsDeleted)).ThenInclude(jr => jr.Skill)
            .Include(j => j.JobScheduleRequirements.Where(js => !js.IsDeleted))
            .Include(j => j.ParentProfile).ThenInclude(p => p.User)
            .ThenInclude(u => u.UserSubscriptions)
            .ThenInclude(s => s.SubscriptionPlan)
            .Include(j => j.ParentProfile).ThenInclude(p => p.ChildProfiles.Where(c => !c.IsDeleted))
            .AsQueryable();

        query = query.Where(j =>
            j.ModerationStatus == (int)JobPostingModerationStatus.Approved &&
            (
                j.Status == (int)JobPostingStatus.Public ||
                (currentUserId.HasValue && j.ParentProfile != null && j.ParentProfile.UserId == currentUserId.Value)
            ));

        if (!string.IsNullOrWhiteSpace(filters.City))
            query = query.Where(j => j.City != null && j.City.ToLower().Contains(filters.City.ToLower()));

        if (!string.IsNullOrWhiteSpace(filters.District))
            query = query.Where(j => j.District != null && j.District.ToLower().Contains(filters.District.ToLower()));

        if (filters.JobType.HasValue)
            query = query.Where(j => j.JobType == filters.JobType);

        if (filters.SalaryMin.HasValue)
            query = query.Where(j => j.SalaryMin >= filters.SalaryMin || j.SalaryNegotiable);

        var skip = (filters.Page - 1) * filters.PageSize;
        return await query
            .OrderByDescending(j => j.ParentProfile!.User.UserSubscriptions.Any(s =>
                !s.IsDeleted &&
                s.Status == 1 &&
                s.EndDate >= nowUtc &&
                s.SubscriptionPlan.Name == "Pro"))
            .ThenByDescending(j => j.ParentProfile!.User.UserSubscriptions.Any(s =>
                !s.IsDeleted &&
                s.Status == 1 &&
                s.EndDate >= nowUtc &&
                (s.SubscriptionPlan.Name == "Plus" || s.SubscriptionPlan.Name == "Pro")))
            .ThenByDescending(j => j.PublishedAt ?? j.CreatedAt)
            .Skip(skip)
            .Take(filters.PageSize)
            .ToListAsync();
    }

    public async Task<List<JobPosting>> searchByTitle(string? title)
    {
        var nowUtc = DateTime.UtcNow;
        var query = _db.JobPostings
            .Where(j => !j.IsDeleted
                && j.Status == (int)JobPostingStatus.Public
                && j.ModerationStatus == (int)JobPostingModerationStatus.Approved)
            .Include(j => j.ChildProfile)
            .Include(j => j.JobRequirements.Where(jr => !jr.IsDeleted)).ThenInclude(jr => jr.Skill)
            .Include(j => j.JobScheduleRequirements.Where(js => !js.IsDeleted))
            .Include(j => j.ParentProfile).ThenInclude(p => p.User)
            .ThenInclude(u => u.UserSubscriptions)
            .ThenInclude(s => s.SubscriptionPlan)
            .Include(j => j.ParentProfile).ThenInclude(p => p.ChildProfiles.Where(c => !c.IsDeleted))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
            query = query.Where(j => j.Title.ToLower().Contains(title.ToLower()));

        return await query
            .OrderByDescending(j => j.ParentProfile!.User.UserSubscriptions.Any(s =>
                !s.IsDeleted &&
                s.Status == 1 &&
                s.EndDate >= nowUtc &&
                s.SubscriptionPlan.Name == "Pro"))
            .ThenByDescending(j => j.ParentProfile!.User.UserSubscriptions.Any(s =>
                !s.IsDeleted &&
                s.Status == 1 &&
                s.EndDate >= nowUtc &&
                (s.SubscriptionPlan.Name == "Plus" || s.SubscriptionPlan.Name == "Pro")))
            .ThenByDescending(j => j.PublishedAt ?? j.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<List<JobPosting>> getListPosting(Guid parentProfileId) =>
        await _db.JobPostings
            .Where(j => j.ParentProfileId == parentProfileId && !j.IsDeleted)
            .Include(j => j.ChildProfile)
            .Include(j => j.JobRequirements.Where(jr => !jr.IsDeleted)).ThenInclude(jr => jr.Skill)
            .Include(j => j.JobScheduleRequirements.Where(js => !js.IsDeleted))
            .Include(j => j.JobApplications)
            .Include(j => j.ParentProfile).ThenInclude(p => p.User)
            .ThenInclude(u => u.UserSubscriptions)
            .ThenInclude(s => s.SubscriptionPlan)
            .Include(j => j.ParentProfile).ThenInclude(p => p.ChildProfiles.Where(c => !c.IsDeleted))
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

    public async Task<JobPosting?> viewDetailPosting(Guid id) =>
        await _db.JobPostings
            .Where(j => j.Id == id && !j.IsDeleted)
            .Include(j => j.ChildProfile)
            .Include(j => j.JobRequirements.Where(jr => !jr.IsDeleted)).ThenInclude(jr => jr.Skill)
            .Include(j => j.JobScheduleRequirements.Where(js => !js.IsDeleted))
            .Include(j => j.ParentProfile).ThenInclude(p => p.User)
            .ThenInclude(u => u.UserSubscriptions)
            .ThenInclude(s => s.SubscriptionPlan)
            .Include(j => j.ParentProfile).ThenInclude(p => p.ChildProfiles.Where(c => !c.IsDeleted))
            .Include(j => j.JobApplications)
            .FirstOrDefaultAsync();

    public async Task<ParentProfile?> getParentProfileSnapshot(Guid parentProfileId) =>
        await _db.ParentProfiles
            .Where(p => p.Id == parentProfileId && !p.IsDeleted)
            .Include(p => p.User)
            .Include(p => p.ChildProfiles.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync();

    public async Task<List<Skill>> getSkillsByNames(IEnumerable<string> names)
    {
        var normalized = names.Select(n => n.ToLower()).ToList();
        return await _db.Skills
            .Where(s => !s.IsDeleted && normalized.Contains(s.Name.ToLower()))
            .ToListAsync();
    }
    public async Task<ParentProfile?> getParentProfileSnapshot(Guid parentProfileId) =>
        await _db.ParentProfiles
            .Where(p => p.Id == parentProfileId && !p.IsDeleted)
            .Include(p => p.User)
            .Include(p => p.ChildProfiles.Where(c => !c.IsDeleted))
            .FirstOrDefaultAsync();

    public async Task<List<Skill>> getSkillsByNames(IEnumerable<string> names)
    {
        var normalized = names.Select(n => n.ToLower()).ToList();
        return await _db.Skills
            .Where(s => !s.IsDeleted && normalized.Contains(s.Name.ToLower()))
            .ToListAsync();
    }

    public async Task<List<Skill>> getActiveSkills() =>
        await _db.Skills
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

    public async Task<List<Skill>> getActiveSkills() =>
        await _db.Skills
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();

    public void addSkills(IEnumerable<Skill> skills) => _db.Skills.AddRange(skills);

    public void addSkills(IEnumerable<Skill> skills) => _db.Skills.AddRange(skills);

    public async Task<JobPosting> createJobPosting(JobPosting job)
    {
        _db.JobPostings.Add(job);
        await _db.SaveChangesAsync();
        return job;
    }

    public async Task updateJobPosting(JobPosting job)
    {
        job.UpdatedAt = DateTime.UtcNow;
        _db.JobPostings.Update(job);
        await _db.SaveChangesAsync();
    }

    public async Task deleteJobPosting(JobPosting job)
    {
        job.IsDeleted = true;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public void removeRequirements(IEnumerable<JobRequirement> reqs) =>
        _db.JobRequirements.RemoveRange(reqs);

    public void addRequirements(IEnumerable<JobRequirement> reqs) =>
        _db.JobRequirements.AddRange(reqs);

    public void removeScheduleRequirements(IEnumerable<JobScheduleRequirement> reqs) =>
        _db.JobScheduleRequirements.RemoveRange(reqs);

    public void addScheduleRequirements(IEnumerable<JobScheduleRequirement> reqs) =>
        _db.JobScheduleRequirements.AddRange(reqs);

    public async Task<int> countJobPostingsInCurrentMonth(Guid parentProfileId)
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await _db.JobPostings
            .Where(j => j.ParentProfileId == parentProfileId && j.CreatedAt >= startOfMonth && !j.IsDeleted)
            .CountAsync();
    }

    public async Task hideExpiredPostings()
    {
        var nowUtc = DateTime.UtcNow;
        var expiredJobs = await _db.JobPostings
            .Where(j => !j.IsDeleted
                && j.Status == (int)JobPostingStatus.Public
                && j.ExpiresAt.HasValue
                && j.ExpiresAt < nowUtc)
            .ToListAsync();

        if (expiredJobs.Count == 0)
            return;

        foreach (var job in expiredJobs)
        {
            job.Status = (int)JobPostingStatus.Hidden;
            job.ClosedAt = nowUtc;
            job.UpdatedAt = nowUtc;
        }

        await _db.SaveChangesAsync();
    }

    public async Task saveChanges() => await _db.SaveChangesAsync();
}
