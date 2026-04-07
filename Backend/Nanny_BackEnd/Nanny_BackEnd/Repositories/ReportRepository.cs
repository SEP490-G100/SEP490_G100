using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class ReportRepository
{
    private readonly Sep490NannyDbContext _db;

    public ReportRepository(Sep490NannyDbContext db) => _db = db;

    public async Task<JobPosting?> GetJobPostingForReportAsync(Guid jobPostingId) =>
        await _db.JobPostings
            .Where(j => j.Id == jobPostingId && !j.IsDeleted)
            .Include(j => j.ParentProfile)
            .FirstOrDefaultAsync();

    public async Task<Message?> GetMessageForReportAsync(Guid messageId) =>
        await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

    public async Task<User?> GetUserForProfileReportAsync(Guid userId) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

    public async Task<Conversation?> GetConversationForReportAsync(Guid conversationId) =>
        await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId && !c.IsDeleted);

    public async Task<bool> IsConversationParticipantAsync(Guid conversationId, Guid userId) =>
        await _db.ConversationParticipants.AnyAsync(p =>
            !p.IsDeleted &&
            p.ConversationId == conversationId &&
            p.UserId == userId);

    public async Task<bool> HasPendingReportAsync(Guid reporterUserId, Guid reportedEntityId, string reportedEntityType) =>
        await _db.Reports.AnyAsync(r =>
            !r.IsDeleted &&
            r.ReporterUserId == reporterUserId &&
            r.ReportedEntityId == reportedEntityId &&
            r.ReportedEntityType == reportedEntityType &&
            r.Status == 0);

    public async Task<(List<Report> Items, int TotalCount)> GetPagedReportsAsync(
        int? status,
        string? entityType,
        string? search,
        int page,
        int pageSize)
    {
        var query = _db.Reports
            .Where(r => !r.IsDeleted)
            .Include(r => r.ReporterUser)
            .Include(r => r.HandledByNavigation)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var type = entityType.Trim();
            query = query.Where(r => r.ReportedEntityType == type);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(r =>
                r.Reason.ToLower().Contains(s) ||
                r.ReportedEntityType.ToLower().Contains(s) ||
                r.ReporterUser.Email.ToLower().Contains(s) ||
                r.ReporterUser.FirstName.ToLower().Contains(s) ||
                r.ReporterUser.LastName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Report?> GetReportByIdAsync(Guid id, bool includeDeleted = false)
    {
        var query = _db.Reports
            .Include(r => r.ReporterUser)
            .Include(r => r.HandledByNavigation)
            .AsQueryable();

        if (!includeDeleted)
            query = query.Where(r => !r.IsDeleted);

        return await query.FirstOrDefaultAsync(r => r.Id == id);
    }

    public void AddReport(Report report) => _db.Reports.Add(report);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
