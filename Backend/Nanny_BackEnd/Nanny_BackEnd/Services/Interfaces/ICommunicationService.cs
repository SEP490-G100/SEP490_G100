using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Communication;

namespace Nanny_BackEnd.Services.Interfaces;

public interface ICommunicationService
{
    Task BroadcastPersistedMessageAsync(Guid messageId);
    Task<List<ConversationDto>> GetConversationsAsync(Guid userId);
    Task<int> GetTotalUnreadMessageCountAsync(Guid userId);
    Task<int> MarkConversationReadAsync(Guid conversationId, Guid userId);
    Task<MessageHistoryDto> GetMessageHistoryAsync(
        Guid conversationId, Guid currentUserId, int page, int pageSize);
    Task<ConversationDto> GetOrCreateConversationAsync(
        Guid currentUserId, Guid otherUserId);
    Task<MessageDto> SendMessageAsync(SendMessageDto dto, Guid senderId);
    Task<(Guid ConversationId, Guid MessageId)> DeleteMessageAsync(Guid messageId, Guid userId);
    Task ReportMessageAsync(Guid messageId, Guid reporterUserId, ReportMessageDto dto);
    Task<(bool IsBlocked, bool IsHidden, Guid? BlockedByUserId)> UpdateConversationStatusAsync(
        Guid conversationId, Guid userId, UpdateConversationStatusDto dto);
}
