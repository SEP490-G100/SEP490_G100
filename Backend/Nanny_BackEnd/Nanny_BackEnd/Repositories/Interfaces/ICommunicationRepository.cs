using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface ICommunicationRepository
{
    Task<List<Conversation>> GetConversationsByUserIdAsync(Guid userId);
    Task<Conversation?> GetConversationByIdAsync(Guid id);
    Task<Conversation?> FindOneToOneConversationAsync(Guid userId1, Guid userId2);
    void AddConversation(Conversation conversation);
    void AddParticipant(ConversationParticipant participant);
    Task<ConversationParticipant?> GetParticipantAsync(Guid conversationId, Guid userId);
    Task<List<ConversationParticipant>> GetParticipantsByConversationIdAsync(Guid conversationId);
    Task<(List<Message> Items, int Total)> GetMessagesByConversationIdAsync(
        Guid conversationId, int skip, int take);
    Task<Message?> GetMessageByIdAsync(Guid messageId);
    Task<Message?> GetMessageByIdForHubAsync(Guid messageId);
    void AddMessage(Message message);
    Task<int> CountUnreadMessagesForUserAsync(Guid userId);
    Task<int> MarkConversationMessagesReadForUserAsync(Guid conversationId, Guid readerUserId);
    void AddReport(Report report);
    Task SaveChangesAsync();
}
