using OneNest.Domain.Entities;

namespace OneNest.Application.Interfaces.Repositories;

public interface IAIConversationRepository
{
    Task<List<AIConversation>> GetAllForUserAsync(Guid userId, bool includeArchived = false);
    Task<List<AIConversation>> SearchConversationsAsync(Guid userId, string query, bool includeArchived = false);
    Task<AIConversation?> GetConversationAsync(Guid id, Guid userId);
    Task<List<AIMessage>> GetMessagesAsync(Guid conversationId, Guid userId);
    Task<List<AIMessage>> GetLastMessagesAsync(Guid conversationId, Guid userId, int limit);
    Task AddConversationAsync(AIConversation conversation);
    Task UpdateConversationAsync(AIConversation conversation);
    Task DeleteConversationAsync(AIConversation conversation);
    Task AddMessageAsync(AIMessage message);
}
