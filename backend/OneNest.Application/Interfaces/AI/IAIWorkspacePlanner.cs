using OneNest.Application.DTOs.AI;

namespace OneNest.Application.Interfaces.AI;

public interface IAIWorkspacePlanner
{
    Task<WorkspaceToolPlanResult> PlanAsync(
        WorkspaceToolExecutionContext context,
        IReadOnlyList<WorkspaceToolDefinition> tools,
        CancellationToken cancellationToken = default);
}
