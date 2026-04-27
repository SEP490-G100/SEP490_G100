using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class CommunicationRepository : ICommunicationRepository
{
    private readonly Sep490NannyDbContext _db;

    public CommunicationRepository(Sep490NannyDbContext db) => _db = db;

    // ─── Conversation ─────────────────────────────────────────────────────────

    public async Task<List<Conversation>> GetConversationsByUserIdAsync(Guid userId) =>
        await _db.Conversations
            .Where(c => !c.IsDeleted &&
                        c.ConversationParticipants.Any(p => p.UserId == userId && !p.IsDeleted))
            .Include(c => c.ConversationParticipants.Where(p => !p.IsDeleted))
                .ThenInclude(p => p.User)
            .Include(c => c.Messages.Where(m => !m.IsDeleted))
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();

    public async Task<Conversation?> GetConversationByIdAsync(Guid id) =>
        await _db.Conversations
            .Where(c => c.Id == id && !c.IsDeleted)
            .Include(c => c.ConversationParticipants.Where(p => !p.IsDeleted))
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync();

    /// <summary>Tìm conversation 1-1 giữa 2 user (tránh tạo duplicate).</summary>
    public async Task<Conversation?> FindOneToOneConversationAsync(Guid userId1, Guid userId2) =>
        await _db.Conversations
            .Where(c =>
                !c.IsDeleted &&
                c.Type == 1 &&
                c.ConversationParticipants.Count(p => !p.IsDeleted) == 2 &&
                c.ConversationParticipants.Any(p => p.UserId == userId1 && !p.IsDeleted) &&
                c.ConversationParticipants.Any(p => p.UserId == userId2 && !p.IsDeleted))
            .Include(c => c.ConversationParticipants.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync();

    public void AddConversation(Conversation conversation) => _db.Conversations.Add(conversation);

    public void AddParticipant(ConversationParticipant participant) =>
        _db.ConversationParticipants.Add(participant);

    // ─── Participant ──────────────────────────────────────────────────────────

    public async Task<ConversationParticipant?> GetParticipantAsync(Guid conversationId, Guid userId) =>
        await _db.ConversationParticipants
            .FirstOrDefaultAsync(p =>
                p.ConversationId == conversationId &&
                p.UserId == userId &&
                !p.IsDeleted);

    public async Task<List<ConversationParticipant>> GetParticipantsByConversationIdAsync(Guid conversationId) =>
        await _db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId && !p.IsDeleted)
            .ToListAsync();

    // ─── Message ──────────────────────────────────────────────────────────────

    public async Task<(List<Message> Items, int Total)> GetMessagesByConversationIdAsync(
        Guid conversationId, int skip, int take) =>
    (
        await _db.Messages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .Include(m => m.SenderUser)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(),
        await _db.Messages.CountAsync(m => m.ConversationId == conversationId && !m.IsDeleted)
    );

    public async Task<Message?> GetMessageByIdAsync(Guid messageId) =>
        await _db.Messages.FindAsync(messageId);

    /// <summary>Load message with sender for SignalR broadcast (after SaveChanges).</summary>
    public async Task<Message?> GetMessageByIdForHubAsync(Guid messageId) =>
        await _db.Messages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .FirstOrDefaultAsync(m => m.Id == messageId && !m.IsDeleted);

    public void AddMessage(Message message) => _db.Messages.Add(message);

    /// <summary>
    /// Tổng số tin chưa đọc (người khác gửi, ReadAt null) trong các hội thoại mà user tham gia.
    /// Cùng quy tắc với <see cref="CommunicationService"/> mapConversation UnreadCount.
    /// </summary>
    public async Task<int> CountUnreadMessagesForUserAsync(Guid userId) =>
        await (
            from m in _db.Messages.AsNoTracking()
            join c in _db.Conversations.AsNoTracking() on m.ConversationId equals c.Id
            join p in _db.ConversationParticipants.AsNoTracking() on m.ConversationId equals p.ConversationId
            where !c.IsDeleted
                  && !m.IsDeleted
                  && m.ReadAt == null
                  && m.SenderUserId != userId
                  && p.UserId == userId
                  && !p.IsDeleted
            select m.Id).Distinct().CountAsync();

    /// <summary>Đánh dấu mọi tin do người khác gửi trong hội thoại là đã đọc (navbar / unread đồng bộ DB).</summary>
    public async Task<int> MarkConversationMessagesReadForUserAsync(Guid conversationId, Guid readerUserId)
    {
        var canAccess = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == readerUserId && !p.IsDeleted);
        if (!canAccess) return 0;

        var now = DateTime.UtcNow;
        return await _db.Messages
            .Where(m =>
                m.ConversationId == conversationId &&
                !m.IsDeleted &&
                m.ReadAt == null &&
                m.SenderUserId != readerUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ReadAt, now)
                .SetProperty(m => m.UpdatedAt, now));
    }

    // ─── Report ───────────────────────────────────────────────────────────────
    public void AddReport(Report report) => _db.Reports.Add(report);

    // ─── Persist ──────────────────────────────────────────────────────────────

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
