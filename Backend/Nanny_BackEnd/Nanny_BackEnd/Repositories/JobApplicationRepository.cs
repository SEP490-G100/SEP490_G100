using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class JobApplicationRepository : IJobApplicationRepository
{
    private readonly Sep490NannyDbContext _db;

    public JobApplicationRepository(Sep490NannyDbContext db) => _db = db;

    // ── NannyProfile ──────────────────────────────────────────────────────

    public async Task<NannyProfile?> GetNannyProfileWithUserAsync(Guid userId) =>
        await _db.NannyProfiles
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.UserId == userId && !n.IsDeleted);

    public async Task<Guid?> GetNannyProfileIdByUserIdAsync(Guid userId) =>
        await _db.NannyProfiles
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .Select(n => (Guid?)n.Id)
            .FirstOrDefaultAsync();

    public async Task<bool> HasApprovedHealthCertificateAsync(Guid nannyProfileId, DateTime utcNow)
    {
        var today = utcNow.Date;

        return await _db.VerificationRequests
            .Where(request =>
                request.NannyProfileId == nannyProfileId &&
                !request.IsDeleted &&
                request.RequestType == (int)VerificationRequestType.HealthCertificate &&
                request.Status == (int)NannyVerificationRequestStatus.Approved)
            .AnyAsync(request => request.VerificationDocuments.Any(document =>
                !document.IsDeleted &&
                document.DocumentType == (int)VerificationDocumentType.HealthCertificate &&
                (!document.ExpiryDate.HasValue || document.ExpiryDate.Value.Date >= today)));
    }

    // ── JobPosting ────────────────────────────────────────────────────────

    public async Task<JobPosting?> GetJobPostingForApplyAsync(Guid jobPostingId) =>
        await _db.JobPostings
            .Include(j => j.ParentProfile)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(j => j.Id == jobPostingId && !j.IsDeleted);

    public async Task<JobPosting?> GetJobPostingForParentAsync(Guid jobPostingId, Guid parentProfileId) =>
        await _db.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(j =>
                j.Id == jobPostingId &&
                j.ParentProfileId == parentProfileId &&
                !j.IsDeleted);

    // ── JobApplication ────────────────────────────────────────────────────

    public async Task<JobApplication?> GetExistingApplicationAsync(Guid jobPostingId, Guid nannyProfileId) =>
        await _db.JobApplications
            .FirstOrDefaultAsync(a =>
                a.JobPostingId == jobPostingId &&
                a.NannyProfileId == nannyProfileId &&
                !a.IsDeleted);

    public async Task<int> CountMonthlyApplicationsAsync(Guid nannyProfileId, DateTime startOfMonth) =>
        await _db.JobApplications.CountAsync(a =>
            a.NannyProfileId == nannyProfileId &&
            !a.IsDeleted &&
            a.CreatedAt >= startOfMonth);

    public void AddApplication(JobApplication application) =>
        _db.JobApplications.Add(application);

    public async Task<(List<JobApplication> Items, int Total)> GetPagedApplicationsForNannyAsync(
        Guid nannyProfileId, int skip, int take)
    {
        var query = _db.JobApplications
            .Where(a => a.NannyProfileId == nannyProfileId && !a.IsDeleted)
            .Include(a => a.JobPosting)
                .ThenInclude(j => j.ParentProfile)
                    .ThenInclude(p => p.User);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, total);
    }

    public async Task<JobApplication?> GetApplicationForWithdrawAsync(
        Guid applicationId, Guid nannyProfileId) =>
        await _db.JobApplications
            .FirstOrDefaultAsync(a =>
                a.Id == applicationId &&
                a.NannyProfileId == nannyProfileId &&
                !a.IsDeleted);

    public async Task<JobApplication?> GetApplicationForReviewAsync(Guid applicationId) =>
        await _db.JobApplications
            .Include(a => a.JobPosting)
            .Include(a => a.NannyProfile)
                .ThenInclude(n => n.User)
            .FirstOrDefaultAsync(a => a.Id == applicationId && !a.IsDeleted);

    // ── ParentProfile ─────────────────────────────────────────────────────

    public async Task<Guid?> GetParentProfileIdByUserIdAsync(Guid userId) =>
        await _db.ParentProfiles
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync();

    public async Task<List<JobApplication>> GetApplicationsByJobPostingAsync(
        Guid jobPostingId, int? status) =>
        await _db.JobApplications
            .Where(a => a.JobPostingId == jobPostingId &&
                        !a.IsDeleted &&
                        (!status.HasValue || a.Status == status.Value))
            .Include(a => a.NannyProfile)
                .ThenInclude(n => n.User)
            .AsNoTracking()
            .OrderBy(a => a.Status == 0 ? 0 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

    // ─────────────────────────────────────────────────────────────────────

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
