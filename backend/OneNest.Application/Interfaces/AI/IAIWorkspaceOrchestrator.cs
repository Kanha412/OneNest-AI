using OneNest.Application.DTOs.AI;

namespace OneNest.Application.Interfaces.AI;

public interface IAIWorkspaceOrchestrator
{
    Task<WorkspaceContextResult> BuildContextAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default);
}