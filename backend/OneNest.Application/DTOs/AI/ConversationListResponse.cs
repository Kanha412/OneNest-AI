namespace OneNest.Application.DTOs.AI;

public class ConversationListResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
}
