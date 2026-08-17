namespace OneNest.Application.DTOs.AI;

public class WorkspaceToolExecutionContext
{
    public string UserPrompt { get; set; } = string.Empty;
    public IReadOnlyList<ConversationMessage> History { get; set; } = Array.Empty<ConversationMessage>();
    public DateTime UtcNow { get; set; }

    /// <summary>
    /// Depth of workspace context to include in AI responses.
    /// Values: "low" | "medium" | "high". Sourced from UserSettings.
    /// Phase 6 — AI Document Intelligence.
    /// </summary>
    public string ContextDepth { get; set; } = "medium";
}

public class WorkspaceToolResult
{
    public string ToolName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
}

public class WorkspaceContextResult
{
    public bool UsedWorkspaceData => ToolResults.Count > 0;
    public string ResponseMode => UsedWorkspaceData ? "workspace" : "general";
    public List<WorkspaceToolResult> ToolResults { get; set; } = new();
    public string ContextBlock { get; set; } = string.Empty;
}

public class WorkspaceToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class WorkspaceToolPlanResult
{
    public List<string> SelectedTools { get; set; } = new();
}