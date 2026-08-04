using System.Text;
using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;

namespace OneNest.Infrastructure.AI;

public class AIWorkspaceOrchestrator : IAIWorkspaceOrchestrator
{
    private readonly IEnumerable<IAIWorkspaceTool> _tools;

    public AIWorkspaceOrchestrator(IEnumerable<IAIWorkspaceTool> tools)
    {
        _tools = tools;
    }

    public async Task<WorkspaceContextResult> BuildContextAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = new WorkspaceContextResult();
        var prompt = context.UserPrompt?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return result;
        }

        var matchedTools = _tools
            .Where(x => x.CanHandle(prompt))
            .DistinctBy(x => x.Name)
            .ToList();

        foreach (var tool in matchedTools)
        {
            try
            {
                var toolResult = await tool.ExecuteAsync(context, cancellationToken);
                if (toolResult is null || !toolResult.Success || string.IsNullOrWhiteSpace(toolResult.Summary))
                {
                    continue;
                }

                result.ToolResults.Add(toolResult);
            }
            catch
            {
                // Ignore individual tool failures and allow general chat fallback.
            }
        }

        if (!result.ToolResults.Any())
        {
            return result;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Workspace data retrieved for this user:");

        foreach (var tool in result.ToolResults)
        {
            builder.AppendLine($"[{tool.ToolName}]");
            builder.AppendLine(tool.Summary);
            builder.AppendLine();
        }

        builder.AppendLine("Use this workspace data as the highest priority for user-specific answers.");
        result.ContextBlock = builder.ToString().Trim();
        return result;
    }
}
