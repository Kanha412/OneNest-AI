using OneNest.Application.DTOs.AI;

namespace OneNest.Application.Interfaces.Services;

public interface IAIService
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
