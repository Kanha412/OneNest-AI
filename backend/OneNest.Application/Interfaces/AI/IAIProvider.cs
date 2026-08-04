using OneNest.Application.DTOs.AI;

namespace OneNest.Application.Interfaces.AI;

public interface IAIProvider
{
    Task<string> GenerateResponseAsync(
        string systemPrompt,
        IReadOnlyList<ConversationMessage> conversation,
        CancellationToken cancellationToken = default);
}
