using OneNest.Application.DTOs.AI;

namespace OneNest.Application.Interfaces.Services;

public interface IAIConversationService
{
    Task<List<ConversationListResponse>> GetConversationsAsync(bool includeArchived = false, string? search = null);
    Task<ConversationResponse?> GetConversationAsync(Guid conversationId);
    Task<ConversationResponse> CreateConversationAsync(CreateConversationRequest request);
    Task<ConversationResponse?> RenameConversationAsync(Guid conversationId, RenameConversationRequest request);
    Task<bool> ArchiveConversationAsync(Guid conversationId);
    Task<bool> UnarchiveConversationAsync(Guid conversationId);
    Task<bool> DeleteConversationAsync(Guid conversationId);
    Task<ChatResponse?> SendMessageAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken = default);
}
