namespace OneNest.Application.DTOs.AI;

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool UsedWorkspaceData { get; set; }
    public string ResponseMode { get; set; } = "general";
    public List<string> WorkspaceToolsUsed { get; set; } = new();
}
