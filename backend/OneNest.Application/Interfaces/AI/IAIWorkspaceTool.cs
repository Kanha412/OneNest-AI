using OneNest.Application.DTOs.AI;

namespace OneNest.Application.Interfaces.AI;

public interface IAIWorkspaceTool
{
    string Name { get; }
    string Description { get; }

    bool CanHandle(string prompt);

    Task<WorkspaceToolResult?> ExecuteAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default);
}