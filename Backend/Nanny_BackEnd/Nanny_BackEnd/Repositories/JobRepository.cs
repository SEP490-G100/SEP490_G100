using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Search;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class JobRepository
{
    private readonly Sep490NannyDbContext _db;

    public JobRepository(Sep490NannyDbContext db) => _db = db;


    public async Task<List<JobPosting>> searchJobPosting(SearchJobRequest filters)
    {
        var query = _db.JobPostings
            .Where(j => !j.IsDeleted && j.Status == 1 && j.ModerationStatus == 2)
            .Include(j => j.JobRequirements).ThenInclude(jr => jr.Skill)
            .Include(j => j.ParentProfile).ThenInclude(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.City))
            query = query.Where(j => j.City != null &&
                j.City.ToLower().Contains(filters.City.ToLower()));

        if (!string.IsNullOrWhiteSpace(filters.District))
            query = query.Where(j => j.District != null &&
                j.District.ToLower().Contains(filters.District.ToLower()));

        if (filters.JobType.HasValue)
            query = query.Where(j => j.JobType == filters.JobType);

        if (filters.SkillId.HasValue)
            query = query.Where(j => j.JobRequirements.Any(jr => jr.SkillId == filters.SkillId));

        if (filters.SalaryMin.HasValue)
            query = query.Where(j => j.SalaryMax >= filters.SalaryMin || j.SalaryNegotiable);

        var skip = (filters.Page - 1) * filters.PageSize;
        return await query
            .OrderByDescending(j => j.PublishedAt)
            .Skip(skip).Take(filters.PageSize)
            .ToListAsync();
    }

    public async Task<List<JobPosting>> getListPosting(Guid parentProfileId) =>
        await _db.JobPostings
            .Where(j => j.ParentProfileId == parentProfileId && !j.IsDeleted)
            .Include(j => j.JobRequirements).ThenInclude(jr => jr.Skill)
            .Include(j => j.JobApplications)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();


    public async Task<JobPosting?> viewDetailPosting(Guid id) =>
        await _db.JobPostings
            .Where(j => j.Id == id && !j.IsDeleted)
            .Include(j => j.JobRequirements).ThenInclude(jr => jr.Skill)
            .Include(j => j.ParentProfile).ThenInclude(p => p.User)
            .Include(j => j.JobApplications)
            .FirstOrDefaultAsync();


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



    public async Task togglePublishPosting(JobPosting job)
    {
        if (job.Status == 1) // Active → Draft
        {
            job.Status = 0;
            job.ClosedAt = DateTime.UtcNow;
        }
        else // Draft/Closed → Active
        {
            job.Status = 1;
            job.PublishedAt = DateTime.UtcNow;
            job.ClosedAt = null;
        }
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }


    public async Task deleteJobPosting(JobPosting job)
    {
        job.IsDeleted = true;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public void RemoveRequirements(IEnumerable<JobRequirement> reqs) =>
        _db.JobRequirements.RemoveRange(reqs);

    public void AddRequirements(IEnumerable<JobRequirement> reqs) =>
        _db.JobRequirements.AddRange(reqs);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
