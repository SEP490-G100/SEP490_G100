using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Dashboard;
using Nanny_BackEnd.Enums;

namespace Nanny_BackEnd.Repositories;

public class ModeratorDashboardRepository
{
    private readonly Sep490NannyDbContext _db;

    public ModeratorDashboardRepository(Sep490NannyDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetTotalUsersCountAsync() =>
        await _db.Users.CountAsync(u => !u.IsDeleted);

    public async Task<int> GetUserCountByRoleAsync(string roleName) =>
        await _db.Users.CountAsync(u => !u.IsDeleted &&
            u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == roleName));

    public async Task<int> GetUserCountByStatusAsync(int status) =>
        await _db.Users.CountAsync(u => !u.IsDeleted && u.Status == status);

    public async Task<int> GetPendingVerificationCountAsync() =>
        await _db.VerificationRequests
            .AsNoTracking()
            .CountAsync(v => !v.IsDeleted && v.Status == (int)NannyVerificationRequestStatus.Pending);

    public async Task<int> GetActiveJobPostingCountAsync() =>
        await _db.JobPostings
            .AsNoTracking()
            .CountAsync(j => !j.IsDeleted && j.Status == 1);

    public async Task<int> GetTotalContractsCountAsync() =>
        await _db.Contracts
            .AsNoTracking()
            .CountAsync(c => !c.IsDeleted);

    public async Task<int> GetPendingJobPostingCountAsync() =>
        await _db.JobPostings
            .AsNoTracking()
            .CountAsync(j => !j.IsDeleted && j.ModerationStatus == (int)JobPostingModerationStatus.Pending);

    public async Task<int> GetPendingReportsCountAsync() =>
        await _db.Reports
            .AsNoTracking()
            .CountAsync(r => !r.IsDeleted && r.Status == 0);

    public async Task<List<DateTime>> GetPendingVerificationDatesSinceAsync(DateTime startDateUtc) =>
        await _db.VerificationRequests
            .AsNoTracking()
            .Where(v => !v.IsDeleted
                        && v.Status == (int)NannyVerificationRequestStatus.Pending
                        && v.CreatedAt >= startDateUtc)
            .Select(v => v.CreatedAt)
            .ToListAsync();

    public async Task<List<DateTime>> GetPendingJobPostingDatesSinceAsync(DateTime startDateUtc) =>
        await _db.JobPostings
            .AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.ModerationStatus == (int)JobPostingModerationStatus.Pending
                        && j.CreatedAt >= startDateUtc)
            .Select(j => j.CreatedAt)
            .ToListAsync();

    public async Task<List<DateTime>> GetPendingReportDatesSinceAsync(DateTime startDateUtc) =>
        await _db.Reports
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.Status == 0 && r.CreatedAt >= startDateUtc)
            .Select(r => r.CreatedAt)
            .ToListAsync();

    public async Task<List<DashboardModerationEventQueryDto>> GetVerificationModerationEventsSinceAsync(DateTime startDateUtc) =>
        await _db.VerificationRequests
            .AsNoTracking()
            .Where(v => !v.IsDeleted
                        && v.ReviewedAt.HasValue
                        && v.ReviewedAt.Value >= startDateUtc
                        && (v.Status == (int)NannyVerificationRequestStatus.Approved
                            || v.Status == (int)NannyVerificationRequestStatus.Rejected))
            .Select(v => new DashboardModerationEventQueryDto
            {
                Date = v.ReviewedAt!.Value,
                Status = v.Status
            })
            .ToListAsync();

    public async Task<List<DashboardModerationEventQueryDto>> GetJobPostingModerationEventsSinceAsync(DateTime startDateUtc) =>
        await _db.JobPostings
            .AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.ModeratedAt.HasValue
                        && j.ModeratedAt.Value >= startDateUtc
                        && (j.ModerationStatus == (int)JobPostingModerationStatus.Approved
                            || j.ModerationStatus == (int)JobPostingModerationStatus.Rejected))
            .Select(j => new DashboardModerationEventQueryDto
            {
                Date = j.ModeratedAt!.Value,
                Status = j.ModerationStatus
            })
            .ToListAsync();

    public async Task<List<DateTime>> GetUserCreatedDatesSinceAsync(DateTime startDateUtc) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.CreatedAt >= startDateUtc)
            .Select(u => u.CreatedAt)
            .ToListAsync();
}
