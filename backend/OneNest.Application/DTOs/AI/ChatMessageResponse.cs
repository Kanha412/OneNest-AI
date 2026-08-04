namespace OneNest.Application.DTOs.AI;

public class ChatMessageResponse
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsError { get; set; }
    public bool UsedWorkspaceData { get; set; }
    public string ResponseMode { get; set; } = "general";
    public List<string> WorkspaceToolsUsed { get; set; } = new();
}
