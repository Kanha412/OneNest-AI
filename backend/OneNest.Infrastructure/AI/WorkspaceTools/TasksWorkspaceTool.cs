using OneNest.Application.DTOs.AI;
using OneNest.Application.Interfaces.AI;
using OneNest.Application.Interfaces.Services;

namespace OneNest.Infrastructure.AI.WorkspaceTools;

public class TasksWorkspaceTool : IAIWorkspaceTool
{
    private static readonly string[] Keywords =
    [
        "task", "tasks", "todo", "to-do", "pending", "overdue", "due"
    ];

    private readonly ITaskService _taskService;

    public TasksWorkspaceTool(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public string Name => "tasks";

    public bool CanHandle(string prompt)
    {
        var text = prompt.ToLowerInvariant();
        return Keywords.Any(text.Contains);
    }

    public async Task<WorkspaceToolResult?> ExecuteAsync(WorkspaceToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        var tasks = await _taskService.GetAllAsync();

        var today = DateOnly.FromDateTime(context.UtcNow);
        var pending = tasks.Where(x => !x.IsCompleted).ToList();
        var overdue = pending.Where(x => x.DueDate.HasValue && x.DueDate.Value < today).ToList();

        var topPending = pending
            .OrderBy(x => x.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(x => x.CreatedAt)
            .Take(5)
            .ToList();

        var lines = new List<string>
        {
            $"Total tasks: {tasks.Count}",
            $"Pending tasks: {pending.Count}",
            $"Overdue tasks: {overdue.Count}"
        };

        if (topPending.Count > 0)
        {
            lines.Add("Top pending:");
            lines.AddRange(topPending.Select(x =>
                $"- {x.Title} (due: {(x.DueDate.HasValue ? x.DueDate.Value.ToString("yyyy-MM-dd") : "no due date")})"));
        }

        return new WorkspaceToolResult
        {
            ToolName = Name,
            Summary = string.Join("\n", lines)
        };
    }
}
