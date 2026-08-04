using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Infrastructure.AI;

public class AIService : IAIService
{
    private readonly IAIConversationService _conversationService;

    public AIService(IAIConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new InvalidOperationException("Request is required.");

        var conversation = await _conversationService.CreateConversationAsync(new CreateConversationRequest());
        var response = await _conversationService.SendMessageAsync(
            conversation.Id,
            new SendMessageRequest { Message = request.Message },
            cancellationToken);

        if (response is null)
            throw new InvalidOperationException("Unable to process chat request.");

        return response;
    }
}
