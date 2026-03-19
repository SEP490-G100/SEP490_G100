using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class CommunicationRepository
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

    public void AddMessage(Message message) => _db.Messages.Add(message);

    // ─── Report ───────────────────────────────────────────────────────────────

    public void AddReport(Report report) => _db.Reports.Add(report);

    // ─── Persist ──────────────────────────────────────────────────────────────

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
