using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class HiringRepository : IHiringRepository
{
    private readonly Sep490NannyDbContext _db;

    public HiringRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<JobPosting?> GetJobPostingByIdAsync(Guid jobPostingId) =>
        await _db.JobPostings
            .Where(j => j.Id == jobPostingId && !j.IsDeleted)
            .Include(j => j.ParentProfile)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync();

    public async Task<List<JobApplication>> GetApplicantsByJobPostingIdAsync(Guid jobPostingId) =>
        await _db.JobApplications
            .Where(a => a.JobPostingId == jobPostingId && !a.IsDeleted)
            .Include(a => a.JobPosting)
                .ThenInclude(j => j.ParentProfile)
            .Include(a => a.NannyProfile)
                .ThenInclude(n => n.User)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public async Task<JobApplication?> GetJobApplicationByIdAsync(Guid id) =>
        await _db.JobApplications
            .Where(a => a.Id == id && !a.IsDeleted)
            .Include(a => a.JobPosting)
                .ThenInclude(j => j.ParentProfile)
                    .ThenInclude(p => p.User)
            .Include(a => a.NannyProfile)
                .ThenInclude(n => n.User)
            .FirstOrDefaultAsync();

    public async Task<ContactRequest?> GetAcceptedContactRequestAsync(Guid contactRequestId) =>
        await _db.ContactRequests
            .Where(r => r.Id == contactRequestId && !r.IsDeleted && r.Status == 1)
            .Include(r => r.ParentProfile)
                .ThenInclude(p => p.User)
            .Include(r => r.NannyProfile)
                .ThenInclude(n => n.User)
            .FirstOrDefaultAsync();

    public async Task<HiringRecord?> GetHiringRecordByIdAsync(Guid id) =>
        await _db.HiringRecords
            .Where(h => h.Id == id && !h.IsDeleted)
            .Include(h => h.JobApplication)
                .ThenInclude(a => a.JobPosting)
            .Include(h => h.ParentProfile)
                .ThenInclude(p => p.User)
            .Include(h => h.NannyProfile)
                .ThenInclude(n => n.User)
            .FirstOrDefaultAsync();

    public async Task<List<HiringRecord>> GetCompletedUnreviewedHiringsForParentAsync(
        Guid parentUserId,
        IReadOnlyCollection<Guid> reviewedHiringRecordIds) =>
        await _db.HiringRecords
            .Where(h =>
                h.ParentProfile.UserId == parentUserId &&
                h.Status == (int)HiringRecordStatus.Completed &&
                !h.IsDeleted &&
                !reviewedHiringRecordIds.Contains(h.Id))
            .Include(h => h.NannyProfile)
                .ThenInclude(n => n.User)
            .Include(h => h.ParentProfile)
            .OrderByDescending(h => h.EndDate)
            .ToListAsync();

    public async Task<List<HiringRecord>> GetHiringRecordsByUserIdAsync(Guid userId) =>
        await _db.HiringRecords
            .Where(h =>
                !h.IsDeleted &&
                (h.ParentProfile.UserId == userId || h.NannyProfile.UserId == userId))
            .Include(h => h.JobApplication)
                .ThenInclude(a => a.JobPosting)
            .Include(h => h.ParentProfile)
                .ThenInclude(p => p.User)
            .Include(h => h.NannyProfile)
                .ThenInclude(n => n.User)
            .Include(h => h.Contracts)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

    public async Task<HiringRecord?> GetLatestHiringRecordByJobApplicationIdAsync(Guid jobApplicationId) =>
        await _db.HiringRecords
            .Where(h => h.JobApplicationId == jobApplicationId && !h.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync();

    public void AddHiringRecord(HiringRecord hiringRecord) => _db.HiringRecords.Add(hiringRecord);
    public void AddJobPosting(JobPosting jobPosting) => _db.JobPostings.Add(jobPosting);
    public void AddJobApplication(JobApplication jobApplication) => _db.JobApplications.Add(jobApplication);

    public async Task<Contract?> GetContractByHiringRecordIdAsync(Guid hiringRecordId) =>
        await _db.Contracts
            .Where(c => c.HiringRecordId == hiringRecordId && !c.IsDeleted)
            .FirstOrDefaultAsync();

    public void AddContract(Contract contract) => _db.Contracts.Add(contract);

    public async Task<ParentProfile?> GetParentProfileByUserIdAsync(Guid userId) =>
        await _db.ParentProfiles
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Include(p => p.User)
            .FirstOrDefaultAsync();

    public void AddNotification(Notification notification) => _db.Notifications.Add(notification);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
